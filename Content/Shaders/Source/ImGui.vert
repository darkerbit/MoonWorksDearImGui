#version 450

layout (set = 2, binding = 0) uniform UniformBlock
{
	mat4 transform;
} Uniforms;

layout (location = 0) in vec2 pos;
layout (location = 1) in vec2 uv;
layout (location = 2) in vec4 col;

layout (location = 0) out vec2 o_uv;
layout (location = 1) out vec4 o_col;

void main()
{
	gl_Position = Uniforms.transform * vec4(pos, 0.0f, 1.0f);

	o_uv = uv;
	o_col = col;
}
