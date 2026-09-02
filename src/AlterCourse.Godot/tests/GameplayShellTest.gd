class_name GameplayShellTest
extends GdUnitTestSuite

const TEST_QUICK_SAVE_PATH := "user://gameplay-shell-test-quick-save.json"
const DEFAULT_QUICK_SAVE_PATH := "user://quick-save.json"
const LEGACY_DEFAULT_QUICK_SAVE_PATH := "user://quick-save-v1.json"


func before_test() -> void:
	_remove_quick_save_files()


func after_test() -> void:
	_remove_quick_save_files()


func test_main_scene_constructs_gameplay_shell() -> void:
	var screen := _create_screen()

	assert_object(screen).is_instanceof(Control)
	assert_object(screen.get_node_or_null("Shell/MapPanel")).is_not_null()
	assert_object(screen.get_node_or_null("Shell/StatusPanel")).is_not_null()
	assert_int(screen.get_node("%RateControls").get_child_count()).is_equal(5)
	assert_str(screen.get_meta("load_error", "")).is_empty()


func test_quick_save_and_load_controls_exist() -> void:
	var screen := _create_screen()

	assert_object(screen.get_node_or_null("%QuickSaveButton")).is_instanceof(Button)
	assert_object(screen.get_node_or_null("%QuickLoadButton")).is_instanceof(Button)


func test_default_quick_save_writes_schema_v2_without_touching_legacy_slot() -> void:
	_write_text(LEGACY_DEFAULT_QUICK_SAVE_PATH, "legacy-slot-sentinel")
	var screen := _create_default_screen()

	screen.call("QuickSave")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("saved")
	assert_bool(FileAccess.file_exists(DEFAULT_QUICK_SAVE_PATH)).is_true()
	var save_json: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(DEFAULT_QUICK_SAVE_PATH)
	)
	assert_int(int(save_json.get("schemaVersion", -1))).is_equal(2)
	assert_str(FileAccess.get_file_as_string(LEGACY_DEFAULT_QUICK_SAVE_PATH)).is_equal(
		"legacy-slot-sentinel"
	)


func test_default_quick_load_falls_back_to_legacy_then_saves_generic_v2() -> void:
	var snapshot_screen := _create_screen()
	snapshot_screen.call("ProcessSyntheticDelta", 0.6)
	snapshot_screen.call("QuickSave")
	_copy_file(TEST_QUICK_SAVE_PATH, LEGACY_DEFAULT_QUICK_SAVE_PATH)
	var legacy_contents := FileAccess.get_file_as_string(LEGACY_DEFAULT_QUICK_SAVE_PATH)

	var screen := _create_default_screen()
	screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("QuickLoad")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(600)
	screen.call("QuickSave")
	assert_bool(FileAccess.file_exists(DEFAULT_QUICK_SAVE_PATH)).is_true()
	var save_json: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(DEFAULT_QUICK_SAVE_PATH)
	)
	assert_int(int(save_json.get("schemaVersion", -1))).is_equal(2)
	assert_str(FileAccess.get_file_as_string(LEGACY_DEFAULT_QUICK_SAVE_PATH)).is_equal(
		legacy_contents
	)


func test_generic_default_quick_save_wins_over_legacy_slot() -> void:
	var snapshot_screen := _create_screen()
	snapshot_screen.call("ProcessSyntheticDelta", 0.6)
	snapshot_screen.call("QuickSave")
	_copy_file(TEST_QUICK_SAVE_PATH, LEGACY_DEFAULT_QUICK_SAVE_PATH)
	snapshot_screen.call("ProcessSyntheticDelta", 0.6)
	snapshot_screen.call("QuickSave")
	_copy_file(TEST_QUICK_SAVE_PATH, DEFAULT_QUICK_SAVE_PATH)

	var screen := _create_default_screen()
	screen.call("QuickLoad")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(1200)


func test_invalid_generic_default_quick_save_does_not_fall_back_to_legacy() -> void:
	var snapshot_screen := _create_screen()
	snapshot_screen.call("ProcessSyntheticDelta", 0.6)
	snapshot_screen.call("QuickSave")
	_copy_file(TEST_QUICK_SAVE_PATH, LEGACY_DEFAULT_QUICK_SAVE_PATH)
	_write_text(DEFAULT_QUICK_SAVE_PATH, "not-json")

	var screen := _create_default_screen()
	screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("QuickLoad")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("load_failed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(1200)


func test_custom_quick_save_path_never_consults_legacy_slot() -> void:
	var snapshot_screen := _create_screen()
	snapshot_screen.call("ProcessSyntheticDelta", 0.6)
	snapshot_screen.call("QuickSave")
	_copy_file(TEST_QUICK_SAVE_PATH, LEGACY_DEFAULT_QUICK_SAVE_PATH)
	_remove_file(TEST_QUICK_SAVE_PATH)

	var screen := _create_screen()
	screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("QuickLoad")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("load_failed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(1200)
	_write_text(TEST_QUICK_SAVE_PATH, "not-json")
	screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("load_failed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(1800)


func test_quick_save_path_cannot_escape_user_boundary() -> void:
	var scene := load("res://Main.tscn") as PackedScene
	var screen: Node = auto_free(scene.instantiate())
	screen.set("QuickSaveUserPath", "user://../outside.json")
	add_child(screen)
	screen.set_process(false)

	assert_str(screen.get_meta("load_error", "")).contains("user://")
	assert_bool((screen.get_node("%TravelButton") as Button).disabled).is_true()
	screen.call("SelectDestination", "vesper-reach")
	assert_str(screen.get_meta("selected_destination", "")).is_empty()


func test_quick_save_load_restores_active_operations_and_continues_scheduler() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")

	for _step in range(5):
		screen.call("ProcessSyntheticDelta", 0.6)

	var saved_time: int = screen.get_meta("simulation_time_milliseconds", -1)
	var saved_integrity: float = screen.get_meta("sensor_integrity", -1.0)
	var saved_repair_progress: float = screen.get_meta("sensor_repair_progress", -1.0)
	assert_int(saved_time).is_equal(3000)
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_float(saved_repair_progress).is_greater(0.0)
	assert_float(saved_repair_progress).is_less(1.0)

	screen.get_node("%QuickSaveButton").emit_signal("pressed")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("saved")
	assert_bool(FileAccess.file_exists(TEST_QUICK_SAVE_PATH)).is_true()
	var created_at_utc: String = screen.get_meta("quick_save_created_at_utc", "")
	assert_str(created_at_utc).is_not_empty()
	screen.get_node("%QuickSaveButton").emit_signal("pressed")
	assert_str(screen.get_meta("quick_save_created_at_utc", "")).is_equal(created_at_utc)

	screen.call("AdvanceUntilNextEvent")
	screen.call("AdvanceUntilNextEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(12000)
	assert_bool(screen.get_meta("travel_active", true)).is_false()

	screen.call("SetSimulationRate", 0.5)
	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(0)
	screen.get_node("%QuickLoadButton").emit_signal("pressed")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(saved_time)
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_str(screen.get_meta("travel_destination", "")).is_equal("vesper-reach")
	assert_float(screen.get_meta("sensor_integrity", -1.0)).is_equal_approx(saved_integrity, 0.0001)
	assert_float(screen.get_meta("sensor_repair_progress", -1.0)).is_equal_approx(
		saved_repair_progress,
		0.0001
	)

	# The selected rate remains 0.5x, while the pre-load fractional carry was discarded.
	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(0)
	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(3100)

	screen.call("AdvanceUntilNextEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(8000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("SensorRepairCompletion")
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	screen.call("AdvanceUntilNextEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(12000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("TravelArrival")
	assert_bool(screen.get_meta("travel_active", true)).is_false()


func test_failed_quick_load_retains_current_projection() -> void:
	var screen := _create_screen()
	screen.call("ProcessSyntheticDelta", 0.6)
	var retained_time: int = screen.get_meta("simulation_time_milliseconds", -1)
	var retained_integrity: float = screen.get_meta("sensor_integrity", -1.0)

	screen.get_node("%QuickLoadButton").emit_signal("pressed")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("load_failed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(retained_time)
	assert_float(screen.get_meta("sensor_integrity", -1.0)).is_equal_approx(
		retained_integrity,
		0.0001
	)
	assert_str(screen.get_node("%Message").text).contains("Quick load failed")


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
	var destination_button := _find_destination_button(screen, "Vesper Reach")

	assert_object(destination_button).is_not_null()
	destination_button.emit_signal("pressed")
	var travel_button := screen.get_node("%TravelButton") as Button
	assert_bool(travel_button.disabled).is_false()
	travel_button.emit_signal("pressed")

	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_str(screen.get_meta("travel_origin", "")).is_equal("dawn-anchor")
	assert_str(screen.get_meta("travel_destination", "")).is_equal("vesper-reach")
	assert_int(screen.get_meta("travel_eta_milliseconds", -1)).is_equal(12000)
	assert_str(screen.get_node("%TravelStatus").text).contains("Dawn Anchor → Vesper Reach")


func test_time_driven_arrival_refreshes_connected_destination_buttons() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")

	for _step in range(20):
		assert_int(screen.call("ProcessSyntheticDelta", 0.6)).is_equal(6)

	assert_bool(screen.get_meta("travel_active", true)).is_false()
	assert_str(screen.get_node("%TravelStatus").text).contains("Vesper Reach")
	assert_bool(_find_destination_button(screen, "Vesper Reach").disabled).is_true()
	assert_bool(_find_destination_button(screen, "Meridian Drift").disabled).is_false()


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
	screen.set("QuickSaveUserPath", TEST_QUICK_SAVE_PATH)
	add_child(screen)
	screen.set_process(false)
	return screen


func _create_default_screen() -> Node:
	var scene := load("res://Main.tscn") as PackedScene
	var screen: Node = auto_free(scene.instantiate())
	add_child(screen)
	screen.set_process(false)
	return screen


func _find_destination_button(screen: Node, display_name: String) -> Button:
	for child in screen.get_node("%DestinationButtons").get_children():
		if child is Button and child.text == display_name:
			return child
	return null


func _copy_file(source_user_path: String, destination_user_path: String) -> void:
	var error := DirAccess.copy_absolute(
		ProjectSettings.globalize_path(source_user_path),
		ProjectSettings.globalize_path(destination_user_path)
	)
	assert_int(error).is_equal(OK)


func _write_text(user_path: String, contents: String) -> void:
	var file := FileAccess.open(user_path, FileAccess.WRITE)
	assert_object(file).is_not_null()
	file.store_string(contents)
	file.close()


func _remove_quick_save_files() -> void:
	_remove_file(TEST_QUICK_SAVE_PATH)
	_remove_file(DEFAULT_QUICK_SAVE_PATH)
	_remove_file(LEGACY_DEFAULT_QUICK_SAVE_PATH)


func _remove_file(user_path: String) -> void:
	if FileAccess.file_exists(user_path):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(user_path))
