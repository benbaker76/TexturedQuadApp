using Blazor.Extensions;
using Blazor.Extensions.Canvas.WebGL;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace TexturedQuadApp.Pages
{
    public class WebGLComponent : ComponentBase
    {
        private WebGLContext _context;

        protected BECanvasComponent _canvasReference;


        [Inject]
        private HttpClient _httpClient { get; set; }

        private const string VS_SOURCE = "attribute vec3 aPos;" +
                                         "attribute vec2 aTexCoord;" +
                                         "varying vec2 vTexCoord;" +

                                         "void main() {" +
                                            "gl_Position = vec4(aPos, 1.0);" +
                                            "vTexCoord = aTexCoord;" +
                                         "}";

        private const string FS_SOURCE = "precision mediump float;" +
                                         "varying vec2 vTexCoord;" +
                                         "uniform sampler2D uTexture;" +

                                         "void main() {" +
                                            "gl_FragColor = texture2D(uTexture, vTexCoord);" +
                                         "}";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            this._context = await this._canvasReference.CreateWebGLAsync(new WebGLContextAttributes
            {
                PowerPreference = WebGLContextAttributes.POWER_PREFERENCE_HIGH_PERFORMANCE
            });

            await this._context.ClearColorAsync(0, 0, 0, 1);
            await this._context.ClearAsync(BufferBits.COLOR_BUFFER_BIT);

            var program = await this.InitProgramAsync(this._context, VS_SOURCE, FS_SOURCE);

            var vertexBuffer = await this._context.CreateBufferAsync();
            await this._context.BindBufferAsync(BufferType.ARRAY_BUFFER, vertexBuffer);

            // Define vertices for a textured quad
            var vertices = new float[]
            {
                // Positions          // Texture Coordinates
                -0.5f, -0.5f, 0.0f,   0.0f, 0.0f,
                 0.5f, -0.5f, 0.0f,   1.0f, 0.0f,
                 0.5f,  0.5f, 0.0f,   1.0f, 1.0f,
                -0.5f,  0.5f, 0.0f,   0.0f, 1.0f
            };

            await this._context.BufferDataAsync(BufferType.ARRAY_BUFFER, vertices, BufferUsageHint.STATIC_DRAW);

            // Specify vertex attributes
            await this._context.VertexAttribPointerAsync(0, 3, DataType.FLOAT, false, 5 * sizeof(float), 0);
            await this._context.VertexAttribPointerAsync(1, 2, DataType.FLOAT, false, 5 * sizeof(float), 3 * sizeof(float));
            await this._context.EnableVertexAttribArrayAsync(0);
            await this._context.EnableVertexAttribArrayAsync(1);

            // Load texture
            var texture = await LoadTextureAsync("blazor.png");

            // Bind the texture
            await this._context.BindTextureAsync(TextureType.TEXTURE_2D, texture);

            // Set the texture uniform in the fragment shader
            var textureLocation = await this._context.GetUniformLocationAsync(program, "uTexture");
            await this._context.UniformAsync(textureLocation, 0);

            // Use the program and draw the textured quad
            await this._context.UseProgramAsync(program);

            await this._context.BeginBatchAsync();
            await this._context.DrawArraysAsync(Primitive.TRIANGLE_FAN, 0, 4); // Use TRIANGLE_FAN for a quad
            await this._context.EndBatchAsync();
        }

        private async Task<WebGLTexture> LoadTextureAsync(string imagePath)
        {
            var imageStream = await _httpClient.GetStreamAsync($"/images/{imagePath}");

            Console.WriteLine($"/images/{imagePath}");

            using (var skBitmap = SKBitmap.Decode(imageStream))
            {
                var texture = await this._context.CreateTextureAsync();

                await this._context.BindTextureAsync(TextureType.TEXTURE_2D, texture);

                // Flip the texture vertically
                await this._context.PixelStoreIAsync(PixelStorageMode.UNPACK_FLIP_Y_WEBGL, 1);

                var pixelData = new byte[skBitmap.Info.BytesSize];
                Marshal.Copy(skBitmap.GetPixels(), pixelData, 0, pixelData.Length);

                await this._context.TexImage2DAsync(
                    Texture2DType.TEXTURE_2D,
                    0,
                    PixelFormat.RGBA,
                    skBitmap.Width,
                    skBitmap.Height,
                    0,
                    PixelFormat.RGBA,
                    PixelType.UNSIGNED_BYTE,
                    pixelData
                );

                await this._context.GenerateMipmapAsync(TextureType.TEXTURE_2D);

                return texture;
            }
        }

        private async Task<WebGLProgram> InitProgramAsync(WebGLContext gl, string vsSource, string fsSource)
        {
            var vertexShader = await this.LoadShaderAsync(gl, ShaderType.VERTEX_SHADER, vsSource);
            var fragmentShader = await this.LoadShaderAsync(gl, ShaderType.FRAGMENT_SHADER, fsSource);

            var program = await gl.CreateProgramAsync();
            await gl.AttachShaderAsync(program, vertexShader);
            await gl.AttachShaderAsync(program, fragmentShader);
            await gl.LinkProgramAsync(program);

            await gl.DeleteShaderAsync(vertexShader);
            await gl.DeleteShaderAsync(fragmentShader);

            if (!await gl.GetProgramParameterAsync<bool>(program, ProgramParameter.LINK_STATUS))
            {
                string info = await gl.GetProgramInfoLogAsync(program);
                throw new Exception("An error occurred while linking the program: " + info);
            }

            return program;
        }

        private async Task<WebGLShader> LoadShaderAsync(WebGLContext gl, ShaderType type, string source)
        {
            var shader = await gl.CreateShaderAsync(type);

            await gl.ShaderSourceAsync(shader, source);
            await gl.CompileShaderAsync(shader);

            if (!await gl.GetShaderParameterAsync<bool>(shader, ShaderParameter.COMPILE_STATUS))
            {
                string info = await gl.GetShaderInfoLogAsync(shader);
                await gl.DeleteShaderAsync(shader);
                throw new Exception("An error occurred while compiling the shader: " + info);
            }

            return shader;
        }
    }
}
