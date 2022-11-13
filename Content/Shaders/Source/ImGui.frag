#version 450

layout (set = 1, binding = 0) uniform sampler2D tex;

layout (location = 0) in vec2 uv;
layout (location = 1) in vec4 col;

layout (location = 0) out vec4 o_col;

void main()
{
	o_col = texture(tex, uv) * col;
}

