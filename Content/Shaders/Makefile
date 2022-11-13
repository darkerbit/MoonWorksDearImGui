SHADERS := ImGui.vert ImGui.frag

SPV := $(SHADERS:%=SPIR-V/%.spv)

all: $(SPV)

SPIR-V/%.spv: Source/%
	glslc -o $@ $<

clean:
	-rm -rf $(SPV)
