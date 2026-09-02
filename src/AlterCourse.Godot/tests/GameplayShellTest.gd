class_name GameplayShellTest
extends GdUnitTestSuite


func test_main_scene_constructs_gameplay_shell() -> void:
	var screen := _create_screen()

	assert_object(screen).is_instanceof(Control)
	assert_object(screen.get_node_or_null("Shell/MapPanel")).is_not_null()
	assert_object(screen.get_node_or_null("Shell/StatusPanel")).is_not_null()
	assert_int(screen.get_node("%RateControls").get_child_count()).is_equal(5)
	assert_str(screen.get_meta("load_error", "")).is_empty()


func test_projection_populates_ship_time_sensors_and_map() -> void:
	var screen := _create_screen()

	assert_str(screen.get_meta("ship_name", "")).is_equal("USS Pathfinder")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(0)
	assert_float(screen.get_meta("sensor_integrity", -1.0)).is_equal_approx(0.4, 0.0001)
	assert_int(screen.get_meta("map_location_count", 0)).is_equal(3)
	assert_int(screen.get_meta("map_route_count", 0)).is_equal(2)
	assert_bool(screen.get_meta("travel_active", true)).is_false()


func test_synthetic_rates_fractional_carry_and_catch_up_cap() -> void:
	var half_rate := _create_screen()
	half_rate.call("SetSimulationRate", 0.5)
	assert_int(half_rate.call("ProcessSyntheticDelta", 0.1)).is_equal(0)
	assert_int(half_rate.call("ProcessSyntheticDelta", 0.1)).is_equal(1)

	var one_rate := _create_screen()
	one_rate.call("SetSimulationRate", 1.0)
	assert_int(one_rate.call("ProcessSyntheticDelta", 0.1)).is_equal(1)

	var double_rate := _create_screen()
	double_rate.call("SetSimulationRate", 2.0)
	assert_int(double_rate.call("ProcessSyntheticDelta", 0.1)).is_equal(2)

	var quad_rate := _create_screen()
	quad_rate.call("SetSimulationRate", 4.0)
	assert_int(quad_rate.call("ProcessSyntheticDelta", 0.1)).is_equal(4)
	assert_int(quad_rate.call("ProcessSyntheticDelta", 30.0)).is_equal(6)
	assert_int(quad_rate.get_meta("simulation_time_milliseconds", -1)).is_equal(1000)


func test_pause_repeated_processing_never_advances_core_time() -> void:
	var screen := _create_screen()
	screen.call("SetSimulationRate", 0.0)

	for iteration in range(20):
		assert_int(screen.call("ProcessSyntheticDelta", 10.0 + iteration)).is_equal(0)

	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(0)


func test_connected_destination_submits_travel_and_refreshes_visible_state() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")

	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_str(screen.get_meta("travel_origin", "")).is_equal("dawn-anchor")
	assert_str(screen.get_meta("travel_destination", "")).is_equal("vesper-reach")
	assert_int(screen.get_meta("travel_eta_milliseconds", -1)).is_equal(12000)
	assert_str(screen.get_node("%TravelStatus").text).contains("Dawn Anchor → Vesper Reach")


func test_advance_until_stops_at_repair_before_arrival() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")
	screen.call("AdvanceUntilNextEvent")

	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(8000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("SensorRepairCompletion")
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_float(screen.get_meta("sensor_integrity", 0.0)).is_equal_approx(1.0, 0.0001)

	screen.call("AdvanceUntilNextEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(12000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("TravelArrival")
	assert_bool(screen.get_meta("travel_active", true)).is_false()
	assert_str(screen.get_node("%TravelStatus").text).contains("Vesper Reach")


func test_tactical_view_course_command_refreshes_continuous_movement() -> void:
	var screen := _create_screen()
	var initial_x: float = screen.get_meta("tactical_x", 0.0)
	var initial_y: float = screen.get_meta("tactical_y", 0.0)
	screen.call("ShowTacticalView")
	screen.call("SetDemonstrationCourse")
	screen.call("SetSimulationRate", 1.0)
	screen.call("ProcessSyntheticDelta", 0.1)

	assert_str(screen.get_meta("active_view", "")).is_equal("tactical")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(45.0, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(2.0, 0.0001)
	assert_float(screen.get_meta("tactical_x", initial_x)).is_greater(initial_x)
	assert_float(screen.get_meta("tactical_y", initial_y)).is_greater(initial_y)


func test_tactical_transform_inverts_north_and_preserves_fractional_positions() -> void:
	var screen := _create_screen()
	var tactical_map := screen.get_node("%TacticalMap") as Control
	tactical_map.size = Vector2(400, 300)
	var origin: Vector2 = screen.call("MapTacticalPosition", 0.0, 0.0)
	var north: Vector2 = screen.call("MapTacticalPosition", 0.0, 1.0)
	var fractional: Vector2 = screen.call("MapTacticalPosition", 0.25, -0.75)

	assert_float(north.y).is_less(origin.y)
	assert_float(fractional.x - origin.x).is_equal_approx(4.5, 0.001)
	assert_float(fractional.y - origin.y).is_equal_approx(13.5, 0.001)


func _create_screen() -> Node:
	var scene := load("res://Main.tscn") as PackedScene
	var screen: Node = auto_free(scene.instantiate())
	add_child(screen)
	screen.set_process(false)
	return screen
