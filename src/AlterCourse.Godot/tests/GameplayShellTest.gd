class_name GameplayShellTest
extends GdUnitTestSuite

const TEST_QUICK_SAVE_PATH := "user://gameplay-shell-test-quick-save.json"
const DEFAULT_QUICK_SAVE_PATH := "user://quick-save.json"
const LEGACY_DEFAULT_QUICK_SAVE_PATH := "user://quick-save-v1.json"
const INVALID_CONTENT_PATH := "user://gameplay-shell-invalid-content.json"


func before_test() -> void:
	_remove_quick_save_files()
	_remove_file(INVALID_CONTENT_PATH)


func after_test() -> void:
	_remove_quick_save_files()
	_remove_file(INVALID_CONTENT_PATH)


func test_main_scene_constructs_gameplay_shell() -> void:
	var screen := _create_screen()

	assert_object(screen).is_instanceof(Control)
	assert_object(screen.get_node_or_null("%TopStatusBar")).is_instanceof(PanelContainer)
	assert_object(screen.get_node_or_null("%WorkspaceHost")).is_instanceof(PanelContainer)
	assert_object(screen.get_node_or_null("%StationTabs")).is_instanceof(PanelContainer)
	assert_object(screen.get_node_or_null("%BottomArea")).is_instanceof(HBoxContainer)
	assert_object(screen.get_node_or_null("%EventLogPanel")).is_instanceof(PanelContainer)
	assert_object(screen.get_node_or_null("%CaptainActionsPanel")).is_instanceof(PanelContainer)
	assert_object(screen.get_node_or_null("%CommandDeckWorkspace")).is_instanceof(Control)
	assert_object(screen.get_node_or_null("%EngineeringWorkspace")).is_instanceof(Control)
	assert_object(screen.theme).is_instanceof(Theme)
	assert_int(screen.get_node("%RateControls").get_child_count()).is_equal(5)
	assert_str(screen.get_node("%AdvanceUntilButton").text).contains("[U]")
	assert_str(screen.get_node("%AdvanceUntilButton").tooltip_text).contains("player-visible")
	assert_str(screen.get_meta("load_error", "")).is_empty()


func test_quick_save_and_load_controls_exist() -> void:
	var screen := _create_screen()

	assert_object(screen.get_node_or_null("%QuickSaveButton")).is_instanceof(Button)
	assert_object(screen.get_node_or_null("%QuickLoadButton")).is_instanceof(Button)


func test_default_live_state_is_command_deck_travel() -> void:
	var screen := _create_screen()

	assert_str(screen.get_meta("data_mode", "")).is_equal("Live")
	assert_str(screen.get_meta("active_workspace", "")).is_equal("command")
	assert_str(screen.get_meta("active_view", "")).is_equal("strategic")
	assert_bool((_command_deck(screen) as Control).visible).is_true()
	assert_bool((_command_deck(screen).get_node("%StrategicMap") as Control).visible).is_true()
	assert_bool((_engineering_workspace(screen) as Control).visible).is_false()


func test_semantic_input_actions_and_theme_states_are_configured() -> void:
	var screen := _create_screen()
	var actions := [
		"view_strategic",
		"view_tactical",
		"toggle_pause",
		"cycle_time_rate",
		"advance_until_event",
		"quick_save",
		"quick_load",
		"engage_selected_travel",
		"set_tactical_course",
	]

	for action in actions:
		assert_bool(InputMap.has_action(action)).is_true()

	for state in ["normal", "hover", "pressed", "disabled", "focus"]:
		assert_object(screen.theme.get_stylebox(state, "Button")).is_not_null()


func test_keyboard_view_pause_rate_and_tactical_course_use_shell_actions() -> void:
	var screen := _create_screen()

	_send_action(screen, "view_tactical")
	assert_str(screen.get_meta("active_view", "")).is_equal("tactical")
	assert_bool((screen.get_node("%TacticalButton") as Button).button_pressed).is_true()
	assert_bool((_command_deck(screen).get_node("%TacticalMap") as Control).visible).is_true()
	_send_action(screen, "toggle_pause")
	assert_float(screen.get_meta("simulation_rate", -1.0)).is_equal(0.0)
	assert_str(screen.get_node("%RateStatus").text).contains("PAUSED")
	_send_action(screen, "toggle_pause")
	assert_float(screen.get_meta("simulation_rate", -1.0)).is_equal(1.0)
	_send_action(screen, "cycle_time_rate")
	assert_float(screen.get_meta("simulation_rate", -1.0)).is_equal(2.0)
	_send_action(screen, "set_tactical_course")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(45.0, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(2.0, 0.0001)


func test_keyboard_travel_advance_save_and_load_match_button_commands() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")

	_send_action(screen, "engage_selected_travel")
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	_send_action(screen, "advance_until_event")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(8000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("sensor repair complete")
	_send_action(screen, "quick_save")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("saved")
	_send_action(screen, "advance_until_event")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(12000)
	_send_action(screen, "quick_load")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(8000)
	assert_str(screen.get_meta("selected_destination", "not-reset")).is_empty()


func test_disabled_keyboard_commands_do_not_submit_hidden_actions() -> void:
	var screen := _create_screen()
	var ready_message: String = screen.get_node("%Message").text

	_send_action(screen, "engage_selected_travel")
	assert_bool(screen.get_meta("travel_active", false)).is_false()
	assert_str(screen.get_node("%Message").text).is_equal(ready_message)
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")
	_send_action(screen, "set_tactical_course")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(0.0, 0.0001)
	assert_str(screen.get_node("%CourseButton").tooltip_text).contains("unavailable")


func test_initial_focus_and_explicit_traversal_follow_visible_context() -> void:
	var screen := _create_screen()
	await get_tree().process_frame

	var command := screen.get_node("%CommandStationButton") as Button
	assert_bool(command.has_focus()).is_true()
	assert_str(str(command.focus_next)).is_not_empty()
	screen.call("ShowTacticalView")
	await get_tree().process_frame
	assert_bool(command.has_focus()).is_true()
	assert_bool((screen.get_node("%CourseButton") as Button).visible).is_true()
	screen.call("ShowEngineeringWorkspace")
	await get_tree().process_frame
	assert_bool((screen.get_node("%EngineeringStationButton") as Button).has_focus()).is_true()
	assert_bool((_engineering_workspace(screen) as Control).visible).is_true()
	assert_bool((_command_deck(screen) as Control).visible).is_false()


func test_container_layout_remains_stable_at_practical_sizes() -> void:
	var screen := _create_screen()
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	for viewport_size in [Vector2(1600, 900), Vector2(1920, 1080), Vector2(2560, 1440)]:
		screen.size = viewport_size
		await get_tree().process_frame
		var top := screen.get_node("%TopStatusBar") as Control
		var workspace := screen.get_node("%WorkspaceHost") as Control
		var stations := screen.get_node("%StationTabs") as Control
		var bottom := screen.get_node("%BottomArea") as Control
		var map := _command_deck(screen).get_node("%MapWorkspace") as Control
		var systems := _command_deck(screen).get_node("%SystemsSpine") as Control
		var inspector := _command_deck(screen).get_node("%ContextInspector") as Control
		assert_bool(top.get_global_rect().intersects(workspace.get_global_rect())).is_false()
		assert_bool(workspace.get_global_rect().intersects(stations.get_global_rect())).is_false()
		assert_bool(stations.get_global_rect().intersects(bottom.get_global_rect())).is_false()
		assert_float(map.size.x).is_greater(systems.size.x)
		assert_float(map.size.x).is_greater(inspector.size.x)
		assert_float(screen.get_node("%Message").size.y).is_greater(0.0)
		assert_float(bottom.get_global_rect().end.x).is_less_equal(screen.get_global_rect().end.x)
		assert_float(bottom.get_global_rect().end.y).is_less_equal(screen.get_global_rect().end.y)
		screen.call("ShowEngineeringWorkspace")
		await get_tree().process_frame
		var technical := _engineering_workspace(screen).get_node(
			"WorkspaceMargin/WorkspaceStack/PrimaryAndInspector/TechnicalWorkspace"
		) as Control
		var hierarchy := _engineering_workspace(screen).get_node(
			"WorkspaceMargin/WorkspaceStack/PrimaryAndInspector/HierarchyPanel"
		) as Control
		var engineering_inspector := _engineering_workspace(screen).get_node(
			"WorkspaceMargin/WorkspaceStack/PrimaryAndInspector/InspectorColumn"
		) as Control
		assert_float(technical.size.x).is_greater(hierarchy.size.x)
		assert_float(technical.size.x).is_greater(engineering_inspector.size.x)
		screen.call("ShowCommandWorkspace")
		await get_tree().process_frame


func test_view_switch_preserves_workspace_geometry_and_persistent_header() -> void:
	var screen := _create_screen()
	await get_tree().process_frame
	var command := _command_deck(screen)
	var workspace_size := (screen.get_node("%WorkspaceHost") as Control).size
	var simulation_identity: int = screen.get_meta("simulation_identity", 0)
	var command_instance_id := command.get_instance_id()

	screen.call("ShowTacticalView")
	await get_tree().process_frame
	assert_object(screen.get_node_or_null("%VesselStatus")).is_instanceof(Label)
	assert_object(screen.get_node_or_null("%SimulationTime")).is_instanceof(Label)
	assert_object(screen.get_node_or_null("%RateStatus")).is_instanceof(Label)
	assert_object(screen.get_node_or_null("%ViewStatus")).is_instanceof(Label)
	assert_vector((screen.get_node("%WorkspaceHost") as Control).size).is_equal(workspace_size)
	assert_int(_command_deck(screen).get_instance_id()).is_equal(command_instance_id)
	assert_int(screen.get_meta("simulation_identity", 0)).is_equal(simulation_identity)


func test_strategic_tactical_switch_preserves_selection_and_map_instances() -> void:
	var screen := _create_screen()
	var strategic_map := _command_deck(screen).get_node("%StrategicMap")
	var tactical_map := _command_deck(screen).get_node("%TacticalMap")
	screen.call("SelectDestination", "vesper-reach")

	screen.call("ShowTacticalView")
	assert_bool((tactical_map as Control).visible).is_true()
	assert_bool((strategic_map as Control).visible).is_false()
	screen.call("ShowStrategicView")

	assert_bool((strategic_map as Control).visible).is_true()
	assert_bool((tactical_map as Control).visible).is_false()
	assert_str(screen.get_meta("selected_destination", "")).is_equal("vesper-reach")
	assert_int(_command_deck(screen).get_node("%StrategicMap").get_instance_id()).is_equal(
		strategic_map.get_instance_id()
	)
	assert_int(_command_deck(screen).get_node("%TacticalMap").get_instance_id()).is_equal(
		tactical_map.get_instance_id()
	)


func test_station_tabs_expose_only_implemented_workspaces_and_track_selection() -> void:
	var screen := _create_screen()
	var unsupported := [
		"%TacticalStationButton",
		"%NavigationStationButton",
		"%ScienceStationButton",
		"%CommsStationButton",
		"%OperationsStationButton",
	]

	assert_bool((screen.get_node("%CommandStationButton") as Button).button_pressed).is_true()
	assert_bool((screen.get_node("%EngineeringStationButton") as Button).disabled).is_false()
	for button_path in unsupported:
		var button := screen.get_node(button_path) as Button
		assert_bool(button.disabled).is_true()
		assert_str(button.tooltip_text).contains("not implemented")

	screen.call("ShowEngineeringWorkspace")
	assert_bool((screen.get_node("%EngineeringStationButton") as Button).button_pressed).is_true()
	assert_bool((screen.get_node("%CommandStationButton") as Button).button_pressed).is_false()
	screen.call("ShowCommandWorkspace")
	assert_bool((screen.get_node("%CommandStationButton") as Button).button_pressed).is_true()


func test_command_engineering_command_preserves_simulation_time_selection_and_context() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("ShowTacticalView")
	screen.call("ProcessSyntheticDelta", 0.6)
	var identity: int = screen.get_meta("simulation_identity", 0)
	var time: int = screen.get_meta("simulation_time_milliseconds", -1)
	var command_instance_id := _command_deck(screen).get_instance_id()
	var engineering_instance_id := _engineering_workspace(screen).get_instance_id()

	screen.call("ShowEngineeringWorkspace")
	assert_str(screen.get_meta("active_workspace", "")).is_equal("engineering")
	assert_int(screen.get_meta("simulation_identity", 0)).is_equal(identity)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(time)
	screen.call("ShowCommandWorkspace")
	assert_str(screen.get_meta("active_view", "")).is_equal("tactical")
	assert_str(screen.get_meta("selected_destination", "")).is_equal("vesper-reach")
	assert_int(screen.get_meta("simulation_identity", 0)).is_equal(identity)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(time)
	assert_int(_command_deck(screen).get_instance_id()).is_equal(command_instance_id)
	assert_int(_engineering_workspace(screen).get_instance_id()).is_equal(engineering_instance_id)


func test_preview_fixtures_use_production_workspaces_and_never_advance_or_submit() -> void:
	var screen := _create_screen()
	var identity: int = screen.get_meta("simulation_identity", 0)
	var time: int = screen.get_meta("simulation_time_milliseconds", -1)

	screen.call("ShowPreview", 1)
	assert_str(screen.get_meta("data_mode", "")).is_equal("TravelPreview")
	assert_str(screen.get_meta("active_workspace", "")).is_equal("command")
	assert_str(_collect_control_text(_command_deck(screen))).contains("BETAZED")
	assert_bool((_find_action_button(screen, "adjust-course") as Button).disabled).is_true()
	assert_int(screen.call("ProcessSyntheticDelta", 20.0)).is_equal(0)
	(_find_action_button(screen, "adjust-course") as Button).emit_signal("pressed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(time)

	screen.call("ShowPreview", 2)
	assert_str(screen.get_meta("data_mode", "")).is_equal("CombatPreview")
	assert_str(screen.get_meta("active_view", "")).is_equal("tactical")
	assert_str(_collect_control_text(_command_deck(screen))).contains("GALOR-CLASS CRUISER")
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()

	screen.call("ShowPreview", 3)
	assert_str(screen.get_meta("data_mode", "")).is_equal("EngineeringPreview")
	assert_str(screen.get_meta("active_workspace", "")).is_equal("engineering")
	assert_str(_collect_control_text(_engineering_workspace(screen))).contains("EPS BUS A-4")
	assert_bool((_find_engineering_action_button(screen, "isolate-eps") as Button).disabled).is_true()
	assert_int(screen.get_meta("simulation_identity", 0)).is_equal(identity)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(time)

	screen.call("RestoreLiveMode")
	assert_str(screen.get_meta("data_mode", "")).is_equal("Live")
	assert_str(screen.get_meta("active_workspace", "")).is_equal("engineering")
	assert_int(screen.get_meta("simulation_identity", 0)).is_equal(identity)


func test_live_workspace_actions_translate_to_existing_typed_commands() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	(_find_action_button(screen, "travel") as Button).emit_signal("pressed")
	assert_bool(screen.get_meta("travel_active", false)).is_true()

	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("ShowTacticalView")
	(_find_action_button(screen, "set-tactical-course") as Button).emit_signal("pressed")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(45.0, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(2.0, 0.0001)
	(_find_action_button(screen, "advance-time") as Button).emit_signal("pressed")
	assert_str(screen.get_meta("advance_status", "")).is_equal("advanced")


func test_live_unsupported_values_and_engineering_actions_are_explicitly_unavailable() -> void:
	var screen := _create_screen()
	assert_str(_collect_control_text(_command_deck(screen))).contains("UNAVAILABLE")
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()

	screen.call("ShowEngineeringWorkspace")
	var engineering_text := _collect_control_text(_engineering_workspace(screen))
	assert_str(engineering_text).contains("NOT PROVIDED BY LIVE SIMULATION")
	assert_bool((_find_engineering_action_button(screen, "allocate-power") as Button).disabled).is_true()


func test_invalid_content_bootstrap_is_fail_closed_and_player_safe() -> void:
	_write_text(INVALID_CONTENT_PATH, "not-json")
	var scene := load("res://Main.tscn") as PackedScene
	var screen: Node = auto_free(scene.instantiate())
	screen.set("ShipDefinitionResourcePath", INVALID_CONTENT_PATH)
	add_child(screen)
	screen.set_process(false)

	assert_str(screen.get_meta("load_error", "")).contains("Gameplay content is unavailable")
	assert_str(screen.get_node("%Message").text).not_contains("not-json")
	assert_str(screen.get_node("%Message").text).not_contains(INVALID_CONTENT_PATH)
	for control_name in [
		"%TravelButton",
		"%CourseButton",
		"%AdvanceUntilButton",
		"%QuickSaveButton",
		"%QuickLoadButton",
		"%StrategicButton",
		"%TacticalButton",
		"%CommandStationButton",
		"%EngineeringStationButton",
	]:
		assert_bool((screen.get_node(control_name) as Button).disabled).is_true()


func test_normal_shell_never_projects_hidden_vessel_or_scheduler_truth() -> void:
	var screen := _create_screen()
	var presented := _collect_control_text(screen)

	assert_str(presented).not_contains("USS Wayfarer")
	assert_str(presented).not_contains("USS Horizon")
	assert_str(presented).not_contains("OrderWake")
	assert_str(presented).not_contains("ScheduledWork")


func test_default_quick_save_writes_schema_v3_without_touching_legacy_slot() -> void:
	_write_text(LEGACY_DEFAULT_QUICK_SAVE_PATH, "legacy-slot-sentinel")
	var screen := _create_default_screen()

	screen.call("QuickSave")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("saved")
	assert_bool(FileAccess.file_exists(DEFAULT_QUICK_SAVE_PATH)).is_true()
	var save_json: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(DEFAULT_QUICK_SAVE_PATH)
	)
	assert_int(int(save_json.get("schemaVersion", -1))).is_equal(3)
	assert_str(save_json.get("simulationRulesVersion", "")).is_equal("active-world-orders-v1")
	assert_str(FileAccess.get_file_as_string(LEGACY_DEFAULT_QUICK_SAVE_PATH)).is_equal(
		"legacy-slot-sentinel"
	)


func test_default_quick_load_discovers_legacy_slot_path_then_saves_generic_v3() -> void:
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
	assert_int(int(save_json.get("schemaVersion", -1))).is_equal(3)
	assert_str(save_json.get("simulationRulesVersion", "")).is_equal("active-world-orders-v1")
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

	assert_str(screen.get_meta("load_error", "")).contains("Gameplay content is unavailable")
	assert_str(screen.get_node("%Message").text).not_contains("user://")
	assert_bool((screen.get_node("%TravelButton") as Button).disabled).is_true()
	screen.call("SelectDestination", "vesper-reach")
	assert_str(screen.get_meta("selected_destination", "")).is_empty()


func test_quick_save_load_restores_active_operations_and_continues_advancement() -> void:
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

	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
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

	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(8000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("sensor repair complete")
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(12000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("arrival complete")
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
	assert_str(quad_rate.get_meta("advance_status", "")).is_equal("advanced")


func test_pause_repeated_processing_never_advances_core_time() -> void:
	var screen := _create_screen()
	screen.call("SetSimulationRate", 0.0)

	for iteration in range(20):
		assert_int(screen.call("ProcessSyntheticDelta", 10.0 + iteration)).is_equal(0)

	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(0)


func test_connected_destination_submits_travel_and_refreshes_visible_state() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	var travel_button := screen.get_node("%TravelButton") as Button
	assert_bool(travel_button.disabled).is_false()
	travel_button.emit_signal("pressed")

	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_str(screen.get_meta("travel_origin", "")).is_equal("dawn-anchor")
	assert_str(screen.get_meta("travel_destination", "")).is_equal("vesper-reach")
	assert_int(screen.get_meta("travel_eta_milliseconds", -1)).is_equal(12000)
	assert_str(_collect_control_text(_command_deck(screen))).contains("Dawn Anchor")
	assert_str(_collect_control_text(_command_deck(screen))).contains("Vesper Reach")


func test_destination_selection_projects_one_authoritative_pressed_state() -> void:
	var screen := _create_screen()
	assert_str(screen.get_meta("selected_destination", "")).is_empty()
	assert_bool((screen.get_node("%TravelButton") as Button).disabled).is_true()

	screen.call("SelectDestination", "vesper-reach")
	assert_str(screen.get_meta("selected_destination", "")).is_equal("vesper-reach")
	assert_bool((screen.get_node("%TravelButton") as Button).disabled).is_false()
	assert_str(_collect_control_text(_command_deck(screen).get_node("%InspectorContent"))).contains(
		"Vesper Reach"
	)
	assert_str((_find_action_button(screen, "travel") as Button).text).contains("Vesper Reach")
	screen.call("RequestSelectedTravel")
	assert_str(screen.get_meta("selected_destination", "")).is_equal("vesper-reach")

	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_str(screen.get_meta("selected_destination", "not-reset")).is_empty()
	assert_bool((screen.get_node("%TravelButton") as Button).disabled).is_true()


func test_time_driven_arrival_refreshes_command_presentation() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")

	for _step in range(20):
		assert_int(screen.call("ProcessSyntheticDelta", 0.6)).is_equal(6)

	assert_bool(screen.get_meta("travel_active", true)).is_false()
	assert_str(_collect_control_text(_command_deck(screen))).contains("Vesper Reach")
	assert_str(screen.get_meta("selected_destination", "not-reset")).is_empty()
	assert_bool((screen.get_node("%TravelButton") as Button).disabled).is_true()

	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(12000)
	assert_str(screen.get_meta("last_advance_event", "")).is_equal("No pending player event to advance to.")
	assert_str(screen.get_node("%Message").text).not_contains("TravelArrival")
	assert_str(screen.get_node("%Message").text).not_contains("14000")
	assert_str(screen.get_meta("last_advance_event", "")).not_contains("TravelArrival")


func test_fixed_rate_hidden_horizon_arrival_does_not_masquerade_as_player_arrival() -> void:
	var screen := _create_screen()
	var initial_location_count: int = screen.get_meta("map_location_count", 0)
	var initial_route_count: int = screen.get_meta("map_route_count", 0)

	for _step in range(23):
		assert_int(screen.call("ProcessSyntheticDelta", 0.6)).is_equal(6)
	assert_int(screen.call("ProcessSyntheticDelta", 0.2)).is_equal(2)

	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(14000)
	assert_bool(screen.get_meta("travel_active", true)).is_false()
	assert_str(screen.get_meta("travel_origin", "")).is_empty()
	assert_str(screen.get_meta("travel_destination", "")).is_empty()
	assert_int(screen.get_meta("travel_eta_milliseconds", -1)).is_equal(-1)
	assert_int(screen.get_meta("map_location_count", 0)).is_equal(initial_location_count)
	assert_int(screen.get_meta("map_route_count", 0)).is_equal(initial_route_count)
	assert_str(_collect_control_text(_command_deck(screen))).contains("AT LOCATION")


func test_advance_until_stops_at_repair_before_arrival() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")

	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(8000)
	assert_str(screen.get_meta("advance_status", "")).is_equal("advanced")
	assert_str(screen.get_meta("last_advance_event", "")).contains("sensor repair complete")
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_float(screen.get_meta("sensor_integrity", 0.0)).is_equal_approx(1.0, 0.0001)
	assert_str(_collect_control_text(screen.get_node("%EventLogContent"))).contains(
		"Sensor repair completed"
	)

	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(12000)
	assert_str(screen.get_meta("last_advance_event", "")).contains("arrival complete")
	assert_bool(screen.get_meta("travel_active", true)).is_false()
	assert_str(_collect_control_text(_command_deck(screen))).contains("Vesper Reach")
	assert_str(_collect_control_text(screen.get_node("%EventLogContent"))).contains(
		"Strategic travel arrived"
	)


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


func test_tactical_plot_keeps_sustained_course_marker_and_direction_visible() -> void:
	var screen := _create_screen()
	var tactical_map := _command_deck(screen).get_node("%TacticalMap") as Control
	tactical_map.set_anchors_preset(Control.PRESET_TOP_LEFT)
	tactical_map.size = Vector2(400, 300)
	screen.call("ShowTacticalView")
	screen.call("SetDemonstrationCourse")
	screen.call("SetSimulationRate", 1.0)
	for _step in range(100):
		screen.call("ProcessSyntheticDelta", 0.6)

	var x_kilometers: float = screen.get_meta("tactical_x", 0.0)
	var y_kilometers: float = screen.get_meta("tactical_y", 0.0)
	var heading_degrees: float = screen.get_meta("tactical_heading", 0.0)
	var speed_kilometers_per_second: float = screen.get_meta("tactical_speed", 0.0)
	var marker: Vector2 = screen.call("MapTacticalPosition", x_kilometers, y_kilometers)
	var heading_radians := deg_to_rad(heading_degrees)
	var direction_end := marker + Vector2(sin(heading_radians), -cos(heading_radians)) * (
		24.0 + speed_kilometers_per_second * 3.0
	)
	var plot_bounds := Rect2(Vector2.ZERO, tactical_map.size)

	assert_float(x_kilometers).is_greater(70.0)
	assert_float(y_kilometers).is_greater(70.0)
	assert_bool(plot_bounds.has_point(marker)).is_true()
	assert_bool(plot_bounds.has_point(direction_end)).is_true()


func test_tactical_transform_inverts_north_and_preserves_fractional_positions() -> void:
	var screen := _create_screen()
	var tactical_map := _command_deck(screen).get_node("%TacticalMap") as Control
	tactical_map.set_anchors_preset(Control.PRESET_TOP_LEFT)
	tactical_map.size = Vector2(400, 300)
	var origin: Vector2 = screen.call("MapTacticalPosition", 0.0, 0.0)
	var north: Vector2 = screen.call("MapTacticalPosition", 0.0, 1.0)
	var fractional: Vector2 = screen.call("MapTacticalPosition", 0.25, -0.75)

	assert_float(north.y).is_less(origin.y)
	assert_float(fractional.x - origin.x).is_equal_approx(4.5, 0.001)
	assert_float(fractional.y - origin.y).is_equal_approx(13.5, 0.001)


func _send_action(screen: Node, action: StringName) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = true
	screen.call("_UnhandledInput", event)


func _collect_control_text(node: Node) -> String:
	var presented := ""
	if node is Label or node is Button:
		presented += str(node.text) + " "
	if node is Control:
		presented += str(node.tooltip_text) + " "
	for child in node.get_children():
		presented += _collect_control_text(child)
	return presented


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


func _command_deck(screen: Node) -> Node:
	return screen.get_node("%CommandDeckWorkspace")


func _engineering_workspace(screen: Node) -> Node:
	return screen.get_node("%EngineeringWorkspace")


func _find_action_button(screen: Node, action_id: String) -> Button:
	for child in _command_deck(screen).get_node("%ContextActions").get_children():
		if child is Button and child.name == "Action_" + action_id:
			return child
	return null


func _find_engineering_action_button(screen: Node, action_id: String) -> Button:
	for child in _engineering_workspace(screen).get_node("%EngineeringActionsContent").get_children():
		if child is Button and child.name == "Action_" + action_id.replace("-", "_"):
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
