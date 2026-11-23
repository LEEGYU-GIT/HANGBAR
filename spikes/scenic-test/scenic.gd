extends Node3D
## Throwaway spike for INPC "scenic maps": a columnar-basalt canyon (주상절리) with a waterfall into a pool.
## Everything is generated in code: no imported assets.
## Controls: drag = look around, wheel = zoom, 1/2/3 = quality (low / mid / high), Space = pause the slow drift.
## Screenshot mode: godot --path . -- --shots=<dir>   (saves a few views, then quits)

const R := 0.55                       # hex column circumradius (columns ~1.1 m across, like the real ones)
const POOL_C := Vector2(0.0, -2.0)    # pool centre (x, z)
const POOL_R := Vector2(7.0, 6.0)     # pool radii
const NOTCH := 1.6                    # half-width of the waterfall channel

var env: Environment
var sun: DirectionalLight3D
var cam: Camera3D
var lip := 12.0                       # height the waterfall pours from (set by _basalt)
var yaw := 0.0
var pitch := 0.22
var dist := 25.0
var drift := true
var t := 0.0
var shots_dir := ""
var noise_tex: NoiseTexture2D
var hud: Label

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--shots="):
			shots_dir = a.substr(8)
	noise_tex = _noise(512, false, 0.012)
	_environment()
	_sun()
	_basalt()
	_waterfall()
	_pool()
	_mist()
	_character()
	cam = Camera3D.new()
	cam.fov = 55.0
	cam.far = 800.0
	add_child(cam)
	cam.make_current()
	hud = Label.new()
	hud.position = Vector2(14, 10)
	hud.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	hud.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.6))
	var layer := CanvasLayer.new()
	layer.add_child(hud)
	add_child(layer)
	_set_quality(3 if shots_dir != "" else 2)
	if shots_dir != "":
		hud.visible = false
		_take_shots()

# ---------------------------------------------------------------- look

func _environment() -> void:
	env = Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.22, 0.42, 0.68)
	sky_mat.sky_horizon_color = Color(0.92, 0.86, 0.76)
	sky_mat.ground_horizon_color = Color(0.62, 0.58, 0.5)
	sky_mat.ground_bottom_color = Color(0.2, 0.18, 0.15)
	sky_mat.sun_angle_max = 25.0
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_color = Color(0.95, 0.88, 0.78)
	env.ambient_light_sky_contribution = 0.55     # half sky, half warm fill: less blue in the shade
	env.ambient_light_energy = 1.4
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.35
	env.tonemap_white = 6.0
	env.glow_enabled = true
	env.glow_intensity = 0.4
	env.glow_bloom = 0.03
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.12
	env.volumetric_fog_density = 0.008
	env.volumetric_fog_albedo = Color(0.96, 0.92, 0.86)
	env.volumetric_fog_anisotropy = 0.55
	env.volumetric_fog_length = 90.0
	env.fog_enabled = true                         # cheap distance haze for the low setting
	env.fog_light_color = Color(0.72, 0.78, 0.82)
	env.fog_density = 0.004
	env.fog_aerial_perspective = 0.6
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)

func _sun() -> void:
	sun = DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-30, 120, 0)    # low sun from behind-left: lit column faces, shafts through the mist
	sun.light_color = Color(1.0, 0.88, 0.7)
	sun.light_energy = 2.0
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 140.0
	sun.light_volumetric_fog_energy = 2.2
	add_child(sun)

func _set_quality(q: int) -> void:
	# 1 = plain, 2 = + ambient occlusion and volumetric fog, 3 = + SDFGI bounce light
	env.ssao_enabled = q >= 2
	env.volumetric_fog_enabled = q >= 2
	env.sdfgi_enabled = q >= 3
	env.sdfgi_use_occlusion = true
	env.fog_enabled = q < 2
	get_viewport().msaa_3d = Viewport.MSAA_2X if q >= 2 else Viewport.MSAA_DISABLED
	if hud:
		hud.text = "화질 %d (1/2/3)  ·  끌기: 둘러보기  ·  휠: 확대  ·  Space: 자동 회전" % q

func _noise(size: int, normal: bool, freq: float) -> NoiseTexture2D:
	var n := FastNoiseLite.new()
	n.frequency = freq
	n.fractal_octaves = 5
	var tex := NoiseTexture2D.new()
	tex.width = size
	tex.height = size
	tex.seamless = true
	tex.as_normal_map = normal
	tex.bump_strength = 6.0
	tex.noise = n
	return tex

# ---------------------------------------------------------------- terrain

func _hash(x: int, y: int) -> float:
	var h := (x * 374761393 + y * 668265263) & 0x7fffffff
	h = ((h ^ (h >> 13)) * 1274126177) & 0x7fffffff
	return float(h ^ (h >> 16)) / 2147483647.0

func _height(x: float, z: float, qi: int, ri: int) -> float:
	var e := Vector2((x - POOL_C.x) / POOL_R.x, (z - POOL_C.y) / POOL_R.y).length()
	var jit := _hash(qi, ri) - 0.5
	if e < 1.0:
		if e < 0.8:
			return -99.0                               # open water
		return -0.7 + (e - 0.8) * 3.0 + jit * 0.2      # submerged steps at the pool edge
	var back := smoothstep(-4.0, -9.0, z)
	var front := smoothstep(2.0, 6.0, z)
	var h_side := 4.0 + (e - 1.0) * 11.0
	var h_back := 13.0 + (e - 1.0) * 5.0
	var h_front := 0.25 + (e - 1.0) * 3.2
	var h: float = lerp(lerp(h_side, h_back, back), h_front, front)
	h = minf(h, 26.0)
	h += jit * (1.6 - front * 1.1)
	if z < -6.5 and z > -12.5 and absf(x) < NOTCH:
		return lip - 0.25                            # flat channel floor: the water leaves one clean edge
	return snappedf(h, 0.3)                          # stepped tops, like the real thing

func _basalt() -> void:
	# lip: the back wall height right behind the notch
	lip = snappedf(13.0 + 0.35 * 5.0, 0.3) - 1.2
	var cols: Array = []
	var trees: Array = []
	var sx := sqrt(3.0) * R
	var sz := 1.5 * R
	for ri in range(-30, 20):
		for qi in range(-40, 40):
			var x := sx * (qi + ri * 0.5)
			var z := sz * ri
			if absf(x) > 24.0 or z < -26.0 or z > 15.0:
				continue
			var h := _height(x, z, qi, ri)
			if h < -50.0:
				continue
			cols.append(Vector4(x, z, h, _hash(qi + 7, ri - 3)))
			if h > 6.0 and _hash(qi * 3, ri * 5) > 0.94:
				trees.append(Vector4(x, z, h, _hash(qi, ri * 9)))

	var mesh := CylinderMesh.new()
	mesh.top_radius = R * 0.965
	mesh.bottom_radius = R * 0.965
	mesh.height = 1.0
	mesh.radial_segments = 6
	mesh.rings = 1
	var mat := ShaderMaterial.new()
	mat.shader = Shader.new()
	mat.shader.code = BASALT_SHADER
	mat.set_shader_parameter("noise_tex", noise_tex)
	mesh.material = mat

	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.mesh = mesh
	mm.instance_count = cols.size()
	var bottom := -4.0
	for i in cols.size():
		var c: Vector4 = cols[i]
		var height := c.z - bottom
		var tilt := Basis(Vector3(c.w - 0.5, 0, 0.3).normalized(), (c.w - 0.5) * 0.04)   # barely-off columns
		var b := tilt.scaled(Vector3(1, height, 1))
		mm.set_instance_transform(i, Transform3D(b, Vector3(c.x, bottom + height * 0.5, c.y)))
		var shade := 0.82 + c.w * 0.3
		mm.set_instance_color(i, Color(shade, shade * 0.98, shade * 0.95))
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	add_child(mmi)

	# round trees on the cliff tops (round things against the hex rock)
	var leaf := StandardMaterial3D.new()
	leaf.albedo_color = Color(0.3, 0.5, 0.17)
	leaf.roughness = 1.0
	leaf.metallic_specular = 0.1
	var sphere := SphereMesh.new()
	sphere.radius = 0.5
	sphere.height = 1.0
	sphere.material = leaf
	var tm := MultiMesh.new()
	tm.transform_format = MultiMesh.TRANSFORM_3D
	tm.mesh = sphere
	tm.instance_count = trees.size() * 2
	for i in trees.size():
		var tr: Vector4 = trees[i]
		var s := 0.9 + tr.w * 0.9
		tm.set_instance_transform(i * 2, Transform3D(Basis().scaled(Vector3(s, s * 0.85, s)), Vector3(tr.x, tr.z + s * 0.36, tr.y)))
		var s2 := s * 0.7
		tm.set_instance_transform(i * 2 + 1, Transform3D(Basis().scaled(Vector3(s2, s2, s2)), Vector3(tr.x + s * 0.35, tr.z + s * 0.75, tr.y - 0.2)))
	var tmi := MultiMeshInstance3D.new()
	tmi.multimesh = tm
	add_child(tmi)

	# pool floor
	var floor_mesh := PlaneMesh.new()
	floor_mesh.size = Vector2(60, 60)
	var floor_mat := StandardMaterial3D.new()
	floor_mat.albedo_color = Color(0.09, 0.1, 0.1)
	floor_mat.roughness = 1.0
	floor_mesh.material = floor_mat
	var fl := MeshInstance3D.new()
	fl.mesh = floor_mesh
	fl.position = Vector3(0, -3.0, -2)
	add_child(fl)

# ---------------------------------------------------------------- water

const BRINK_Z := -7.75                # front face of the first channel column row
var land := Vector3.ZERO              # where the falling water meets the pool

func _fall_path() -> PackedVector3Array:
	# One continuous path: flat stream -> rounded brink -> drop leaning slightly out -> below the surface.
	var pts := PackedVector3Array()
	var top := lip - 0.2
	var r := 0.8
	for i in 8:
		pts.append(Vector3(0, top, BRINK_Z - 2.4 + 2.4 * i / 8.0))
	for i in 13:
		var a := PI * 0.5 * i / 12.0
		pts.append(Vector3(0, top - r * (1.0 - cos(a)), BRINK_Z + r * sin(a)))
	var y0 := top - r
	var z1 := BRINK_Z + r
	for i in range(1, 41):
		var f := i / 40.0
		pts.append(Vector3(0, lerpf(y0, -0.6, f), z1 + 0.9 * f * f))
	var fs := y0 / (y0 + 0.6)                          # path fraction where y crosses 0
	land = Vector3(0, 0, z1 + 0.9 * fs * fs)
	return pts

func _waterfall() -> void:
	var mat := ShaderMaterial.new()
	mat.shader = Shader.new()
	mat.shader.code = FALL_SHADER
	mat.set_shader_parameter("noise_tex", noise_tex)
	var path := _fall_path()
	var total := 0.0
	var along := PackedFloat32Array([0.0])
	for k in range(1, path.size()):
		total += path[k].distance_to(path[k - 1])
		along.append(total)
	var rows := path.size() - 1
	var cols := 14
	for layer in 2:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		var w := NOTCH * (0.95 if layer == 0 else 0.82)
		var push := 0.12 * layer                        # second sheet slightly in front
		for j in path.size():
			var p := path[j]
			var s := along[j] / total
			for i in cols + 1:
				var u := float(i) / cols
				var x := (u - 0.5) * 2.0 * w * (1.0 + maxf(0.0, s - 0.2) * 0.3)
				st.set_uv(Vector2(u, along[j] * 0.22 + layer * 0.37))
				st.set_color(Color(s, 1, 1, 1.0 - layer * 0.35))   # r = position along the path
				st.add_vertex(Vector3(x, p.y, p.z + push))
		for j in rows:
			for i in cols:
				var a := j * (cols + 1) + i
				st.add_index(a)
				st.add_index(a + cols + 1)
				st.add_index(a + 1)
				st.add_index(a + 1)
				st.add_index(a + cols + 1)
				st.add_index(a + cols + 2)
		st.generate_normals()
		var mi := MeshInstance3D.new()
		mi.mesh = st.commit()
		mi.material_override = mat
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(mi)
	# the stream on top that feeds it
	var stream := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(NOTCH * 2.2, 4.4)
	stream.mesh = pm
	var smat := ShaderMaterial.new()
	smat.shader = Shader.new()
	smat.shader.code = WATER_SHADER
	smat.set_shader_parameter("normal_a", _noise(256, true, 0.03))
	smat.set_shader_parameter("normal_b", _noise(256, true, 0.05))
	smat.set_shader_parameter("flow", Vector2(0.0, -0.6))
	stream.material_override = smat
	stream.position = Vector3(0, lip - 0.22, BRINK_Z - 2.2)    # flush with the start of the sheet
	add_child(stream)
	_splash()

func _splash() -> void:
	# white water thrown up where the sheet hits the pool: hides the join and sells the impact
	var p := GPUParticles3D.new()
	p.amount = 260
	p.lifetime = 1.1
	p.preprocess = 1.0
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = Vector3(NOTCH * 1.1, 0.05, 0.35)
	pm.direction = Vector3(0, 1, 0.5)
	pm.spread = 40.0
	pm.initial_velocity_min = 1.5
	pm.initial_velocity_max = 4.0
	pm.gravity = Vector3(0, -9.8, 0)
	pm.scale_min = 0.35
	pm.scale_max = 0.9
	var fade := Gradient.new()
	fade.set_color(0, Color(1, 1, 1, 0.9))
	fade.set_color(1, Color(1, 1, 1, 0.0))
	var ramp := GradientTexture1D.new()
	ramp.gradient = fade
	pm.color_ramp = ramp
	p.process_material = pm
	var quad := QuadMesh.new()
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	qm.vertex_color_use_as_albedo = true
	var g := Gradient.new()
	g.set_color(0, Color(1, 1, 1, 0.75))
	g.set_color(1, Color(1, 1, 1, 0.0))
	var gt := GradientTexture2D.new()
	gt.gradient = g
	gt.fill = GradientTexture2D.FILL_RADIAL
	gt.fill_from = Vector2(0.5, 0.5)
	gt.fill_to = Vector2(0.5, 0.0)
	qm.albedo_texture = gt
	quad.material = qm
	p.draw_pass_1 = quad
	p.position = land + Vector3(0, 0.05, 0)
	add_child(p)

func _pool() -> void:
	var water := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(POOL_R.x * 2.4, POOL_R.y * 2.4)
	pm.subdivide_width = 8
	pm.subdivide_depth = 8
	water.mesh = pm
	var mat := ShaderMaterial.new()
	mat.shader = Shader.new()
	mat.shader.code = WATER_SHADER
	mat.set_shader_parameter("normal_a", _noise(256, true, 0.03))
	mat.set_shader_parameter("normal_b", _noise(256, true, 0.05))
	mat.set_shader_parameter("fall_pos", Vector2(land.x, land.z))
	water.material_override = mat
	water.position = Vector3(POOL_C.x, 0.0, POOL_C.y)
	add_child(water)

func _mist() -> void:
	var base := land + Vector3(0, 0.6, 0.6)
	var p := GPUParticles3D.new()
	p.amount = 220
	p.lifetime = 4.0
	p.preprocess = 4.0
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 1.6
	pm.direction = Vector3(0, 1, 0.4)
	pm.spread = 70.0
	pm.initial_velocity_min = 0.6
	pm.initial_velocity_max = 2.2
	pm.gravity = Vector3(0, 0.15, 0)
	pm.damping_min = 0.4
	pm.damping_max = 0.8
	pm.scale_min = 1.5
	pm.scale_max = 3.5
	var fade := Gradient.new()
	fade.set_color(0, Color(1, 1, 1, 0.0))
	fade.add_point(0.25, Color(1, 1, 1, 1.0))
	fade.set_color(fade.get_point_count() - 1, Color(1, 1, 1, 0.0))
	var ramp := GradientTexture1D.new()
	ramp.gradient = fade
	pm.color_ramp = ramp
	p.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(1, 1)
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	qm.vertex_color_use_as_albedo = true
	var g := Gradient.new()
	g.set_color(0, Color(1, 1, 1, 0.22))
	g.set_color(1, Color(1, 1, 1, 0.0))
	var gt := GradientTexture2D.new()
	gt.gradient = g
	gt.fill = GradientTexture2D.FILL_RADIAL
	gt.fill_from = Vector2(0.5, 0.5)
	gt.fill_to = Vector2(0.5, 0.0)
	qm.albedo_texture = gt
	quad.material = qm
	p.draw_pass_1 = quad
	p.position = base
	add_child(p)
	# a pocket of thicker fog at the foot of the falls: catches the sun shafts
	var fog := FogVolume.new()
	fog.size = Vector3(9, 5, 7)
	fog.shape = RenderingServer.FOG_VOLUME_SHAPE_ELLIPSOID
	var fm := FogMaterial.new()
	fm.density = 0.35
	fm.albedo = Color(0.92, 0.95, 1.0)
	fog.material = fm
	fog.position = base + Vector3(0, 1.0, 1.0)
	add_child(fog)

# ---------------------------------------------------------------- our goofy friend, for scale (1.75 m)

func _character() -> void:
	# stand on the front steps, at the column nearest (2.2, 4.6)
	var sx := sqrt(3.0) * R
	var sz := 1.5 * R
	var best := Vector3.ZERO
	var bd := 1e9
	for ri in range(0, 12):
		for qi in range(-8, 8):
			var x := sx * (qi + ri * 0.5)
			var z := sz * ri
			var d := Vector2(x - 2.2, z - 4.6).length()
			if d < bd:
				var h := _height(x, z, qi, ri)
				if h > -50.0:
					bd = d
					best = Vector3(x, h, z)
	var g := Node3D.new()
	var skin := StandardMaterial3D.new()
	skin.albedo_color = Color(1.0, 0.86, 0.74)
	var shirt := StandardMaterial3D.new()
	shirt.albedo_color = Color(0.91, 0.38, 0.17)
	var dark := StandardMaterial3D.new()
	dark.albedo_color = Color(0.12, 0.12, 0.12)

	var leg := CylinderMesh.new()
	leg.top_radius = 0.07
	leg.bottom_radius = 0.07
	leg.height = 0.55
	var ball := SphereMesh.new()
	_part(g, leg, dark, Vector3(-0.12, 0.28, 0))
	_part(g, leg, dark, Vector3(0.12, 0.28, 0), Vector3.ONE, Vector3(0, 0, 0.2))
	_part(g, ball, shirt, Vector3(0, 0.88, 0), Vector3(0.6, 0.8, 0.55))
	_part(g, ball, skin, Vector3(0, 1.42, 0.02), Vector3(0.66, 0.66, 0.66))
	_part(g, ball, dark, Vector3(-0.11, 1.48, -0.3), Vector3(0.09, 0.09, 0.09))
	_part(g, ball, dark, Vector3(0.13, 1.45, -0.3), Vector3(0.09, 0.09, 0.09))
	var arm := CylinderMesh.new()
	arm.top_radius = 0.06
	arm.bottom_radius = 0.06
	arm.height = 0.55
	_part(g, arm, skin, Vector3(-0.32, 1.05, 0), Vector3.ONE, Vector3(0, 0, -0.5))
	_part(g, arm, skin, Vector3(0.3, 1.25, -0.1), Vector3.ONE, Vector3(-0.3, 0, 2.6))   # waving at the falls
	g.position = best
	g.rotation.y = 0.15
	add_child(g)

func _part(g: Node3D, mesh: Mesh, mat: Material, pos: Vector3, scl: Vector3 = Vector3.ONE, rot: Vector3 = Vector3.ZERO) -> void:
	var m := MeshInstance3D.new()
	m.mesh = mesh
	m.material_override = mat
	m.position = pos
	m.scale = scl
	m.rotation = rot
	g.add_child(m)

# ---------------------------------------------------------------- camera and input

func _place_camera() -> void:
	var target := Vector3(0, 5.5, -3.5)
	var p := target + Vector3(sin(yaw) * cos(pitch), sin(pitch), cos(yaw) * cos(pitch)) * dist
	cam.position = p
	cam.look_at(target)

func _process(delta: float) -> void:
	t += delta
	if shots_dir == "" and drift:
		yaw = sin(t * 0.06) * 0.55
	_place_camera()

func _unhandled_input(e: InputEvent) -> void:
	if e is InputEventMouseMotion and (e.button_mask & MOUSE_BUTTON_MASK_LEFT):
		drift = false
		yaw -= e.relative.x * 0.005
		pitch = clampf(pitch + e.relative.y * 0.004, -0.05, 1.2)
	elif e is InputEventMouseButton and e.pressed:
		if e.button_index == MOUSE_BUTTON_WHEEL_UP:
			dist = maxf(8.0, dist - 1.5)
		elif e.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			dist = minf(60.0, dist + 1.5)
	elif e is InputEventKey and e.pressed:
		match e.keycode:
			KEY_1: _set_quality(1)
			KEY_2: _set_quality(2)
			KEY_3: _set_quality(3)
			KEY_SPACE: drift = not drift

func _take_shots() -> void:
	DirAccess.make_dir_recursive_absolute(shots_dir)
	var views := [[0.0, 0.22, 25.0], [-0.5, 0.3, 30.0], [0.55, 0.12, 20.0], [0.12, 0.05, 11.0]]
	await get_tree().create_timer(4.0).timeout          # let SDFGI and the particles settle
	for i in views.size():
		yaw = views[i][0]
		pitch = views[i][1]
		dist = views[i][2]
		_place_camera()
		await get_tree().create_timer(2.5).timeout
		var img := get_viewport().get_texture().get_image()
		img.save_png("%s/view%d.png" % [shots_dir, i + 1])
		print("[shot] view%d" % (i + 1))
	print("[shot] fps=%d" % Engine.get_frames_per_second())
	get_tree().quit()

# ---------------------------------------------------------------- shaders

const BASALT_SHADER := """
shader_type spatial;
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;
varying vec3 wpos;
varying vec3 wnrm;
void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}
void fragment() {
	// flat faces: the cylinder mesh has smooth normals, which makes a hex column read as round
	NORMAL = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));
	vec3 fn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float up = smoothstep(0.55, 0.95, fn.y);
	vec2 side_uv = vec2(wpos.x + wpos.z, wpos.y) * 0.35;
	float n = mix(texture(noise_tex, side_uv).r, texture(noise_tex, wpos.xz * 0.3).r, up);
	float n2 = texture(noise_tex, wpos.xz * 0.06 + 0.37).r;
	vec3 rock = mix(vec3(0.13, 0.12, 0.11), vec3(0.3, 0.27, 0.24), n) * COLOR.rgb;
	// horizontal joints across the columns
	float joint = smoothstep(0.93, 0.99, fract(wpos.y * 0.7 + n * 0.5)) * (1.0 - up);
	rock *= 1.0 - joint * 0.45;
	// rust streaks and moss on the tops
	rock = mix(rock, vec3(0.3, 0.2, 0.13), smoothstep(0.62, 0.8, n) * 0.35 * (1.0 - up));
	float moss = up * smoothstep(0.3, 0.55, n2) * smoothstep(0.5, 2.0, wpos.y);
	vec3 col = mix(rock, mix(vec3(0.26, 0.38, 0.1), vec3(0.45, 0.52, 0.18), n), moss);
	// wet near the water line
	float wet = 1.0 - smoothstep(0.0, 0.9, wpos.y);
	col *= 1.0 - wet * 0.35;
	ALBEDO = col;
	ROUGHNESS = mix(0.88, 0.25, wet);
	SPECULAR = 0.4;
}
"""

const FALL_SHADER := """
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, specular_schlick_ggx;
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;
void fragment() {
	float s1 = texture(noise_tex, vec2(UV.x * 1.3, UV.y * 0.35 - TIME * 0.9)).r;
	float s2 = texture(noise_tex, vec2(UV.x * 3.1 + 0.3, UV.y * 0.7 - TIME * 1.5)).r;
	float streak = s1 * 0.55 + s2 * 0.45;
	// ragged sides and a soft top so the sheet reads as falling water, not a lit panel
	float edge = smoothstep(0.0, 0.22 + 0.15 * s2, UV.x) * smoothstep(1.0, 0.78 - 0.15 * s1, UV.x);
	// the flat run is covered by the stream surface; the sheet fades in over the brink instead of a bright edge
	float top = smoothstep(0.1, 0.24, COLOR.r);
	vec3 c = mix(vec3(0.36, 0.5, 0.56), vec3(0.95, 0.97, 1.0), smoothstep(0.4, 0.75, streak));
	ALBEDO = c;
	EMISSION = c * 0.015;
	ROUGHNESS = 0.2;
	ALPHA = clamp(0.12 + smoothstep(0.32, 0.78, streak) * 0.85, 0.0, 1.0) * edge * top * COLOR.a;
}
"""

const WATER_SHADER := """
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_always, specular_schlick_ggx;
uniform sampler2D depth_tex : hint_depth_texture, filter_linear_mipmap;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D normal_a : hint_normal, repeat_enable, filter_linear_mipmap;
uniform sampler2D normal_b : hint_normal, repeat_enable, filter_linear_mipmap;
uniform vec2 flow = vec2(0.02, 0.0);
uniform vec2 fall_pos = vec2(0.0, -1000.0);
varying vec3 wpos;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
	vec3 na = texture(normal_a, wpos.xz * 0.07 + flow * TIME).rgb;
	vec3 nb = texture(normal_b, wpos.xz * 0.11 - vec2(0.0, 0.03) * TIME + flow * TIME * 1.3).rgb;
	vec3 nm = normalize(mix(na, nb, 0.5));
	NORMAL_MAP = nm;
	NORMAL_MAP_DEPTH = 0.6;
	float raw = texture(depth_tex, SCREEN_UV).r;
	vec4 v = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, raw, 1.0);
	v.xyz /= v.w;
	float depth = clamp((-v.z) - (-VERTEX.z), 0.0, 50.0);
	vec2 refr_uv = SCREEN_UV + (nm.xy - 0.5) * 0.02;
	vec3 below = texture(screen_tex, refr_uv).rgb;
	vec3 shallow = vec3(0.16, 0.5, 0.48);
	vec3 deep = vec3(0.02, 0.1, 0.14);
	float d = 1.0 - exp(-depth * 0.45);
	vec3 col = mix(below * shallow * 1.5, deep, d);
	float foam = (1.0 - smoothstep(0.0, 0.15, depth)) * 0.6;
	float churn = 1.0 - smoothstep(0.5, 3.5, length(wpos.xz - fall_pos));
	float fn = texture(normal_a, wpos.xz * 0.35 - vec2(0.0, TIME * 0.4)).r;
	foam = max(foam * 0.8, churn * smoothstep(0.35, 0.6, fn));
	col = mix(col, vec3(0.92, 0.96, 0.98), foam);
	ALBEDO = col;
	ROUGHNESS = mix(0.1, 0.6, foam);
	SPECULAR = 0.3;
	ALPHA = 1.0;
}
"""
