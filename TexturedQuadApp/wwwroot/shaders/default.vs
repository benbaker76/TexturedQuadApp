#version 300 es

in vec3 aPos;
in vec2 aTexCoord;

out vec2 vUV;

void main() {
    gl_Position = vec4(aPos, 1.0);
    vUV = aTexCoord;
}