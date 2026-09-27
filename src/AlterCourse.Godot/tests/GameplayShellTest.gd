class_name GameplayShellTest
extends GdUnitTestSuite

const TEST_QUICK_SAVE_PATH := "user://gameplay-shell-test-quick-save.json"
const DEFAULT_QUICK_SAVE_PATH := "user://quick-save.json"
const LEGACY_DEFAULT_QUICK_SAVE_PATH := "user://quick-save-v1.json"
const INVALID_CONTENT_PATH := "user://gameplay-shell-invalid-content.json"
# Installed identities of the canonical production loadout (content/ships/pathfinder.json). Test-only convenience: the
# shell publishes engineering hooks by installed identity and never by kind, so this mapping must not move into it.
const SENSORS_ID := 2
const IMPULSE_ID := 3
const SHIELDS_ID := 4
const WEAPONS_ID := 5

var _screens_to_free: Array[Node] = []


func before_test() -> void:
	_remove_quick_save_files()
	_remove_file(INVALID_CONTENT_PATH)


func after_test() -> void:
	for screen in _screens_to_free:
		if is_instance_valid(screen):
			screen.queue_free()
	_screens_to_free.clear()
	await get_tree().process_frame
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


func test_live_course_inputs_apply_stop_and_preserve_draft_on_refresh() -> void:
	var screen := _create_screen()
	screen.call("ShowTacticalView")
	await get_tree().process_frame
	var heading := screen.get_node_or_null("%CourseHeading") as SpinBox
	var speed := screen.get_node_or_null("%CourseSpeed") as SpinBox
	assert_object(heading).is_not_null()
	assert_object(speed).is_not_null()
	if heading == null or speed == null:
		return
	heading.value = 90
	speed.value = 1
	(screen.get_node("%CourseButton") as Button).emit_signal("pressed")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(90.0, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(1.0, 0.0001)
	heading.value = 180
	speed.value = 100
	(screen.get_node("%CourseButton") as Button).emit_signal("pressed")
	assert_str((screen.get_node("%Message") as Label).text).contains("exceeds")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(90.0, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(1.0, 0.0001)
	heading.get_line_edit().grab_focus()
	_advance_fixed_steps(screen, 1)
	await get_tree().process_frame
	assert_float(heading.value).is_equal(180.0)
	assert_float(speed.value).is_equal(100.0)
	assert_bool(heading.get_line_edit().has_focus()).is_true()
	_send_action(screen, "view_strategic")
	assert_str(screen.get_meta("active_view", "")).is_equal("tactical")
	(screen.get_node("%StopCourseButton") as Button).emit_signal("pressed")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(90.0, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal(0.0)
	screen.call("ShowPreview", 2)
	assert_bool((screen.get_node("%CourseButton") as Button).disabled).is_true()
	assert_bool((screen.get_node("%StopCourseButton") as Button).disabled).is_true()
	assert_bool(heading.editable).is_false()
	var clock: int = screen.get_meta("simulation_time_milliseconds", -1)
	(screen.get_node("%StopCourseButton") as Button).emit_signal("pressed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(clock)


func test_live_combat_exposes_core_refusal_and_four_consumer_engineering() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	assert_str(_collect_control_text(_command_deck(screen))).contains("not identified")
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()
	assert_object(_command_deck(screen).get_node_or_null("%TargetSystemSelector")).is_instanceof(OptionButton)
	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "balance") as Button).emit_signal("pressed")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(28)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(20)
	assert_int(screen.get_meta(_installation_meta("allocation", SHIELDS_ID), -1)).is_equal(16)
	assert_int(screen.get_meta(_installation_meta("allocation", WEAPONS_ID), -1)).is_equal(11)
	var text := _collect_control_text(_engineering_workspace(screen))
	assert_str(text).contains("SHIELDS")
	assert_str(text).contains("DIRECTED-ENERGY WEAPONS")


func test_public_controls_approach_identify_stop_allocate_and_fire_with_stable_selection() -> void:
	var screen := _create_screen()
	_prepare_combat_encounter(screen)
	await get_tree().process_frame
	var workspace := _command_deck(screen)
	var selector := workspace.get_node("%TargetSystemSelector") as OptionButton
	var selector_id := selector.get_instance_id()
	assert_int(selector.item_count).is_equal(5)
	var aim_labels: Array[String] = []
	for index in range(selector.item_count):
		aim_labels.append(selector.get_item_text(index))
	assert_array(aim_labels).is_equal(
		["Power generation", "Sensors", "Impulse propulsion", "Shields", "Directed-energy weapons"]
	)
	var selected_index := -1
	for index in range(selector.item_count):
		if selector.get_item_text(index) == "Sensors":
			selected_index = index
	assert_int(selected_index).is_greater_equal(0)
	selector.select(selected_index)
	selector.emit_signal("item_selected", selected_index)
	selector.grab_focus()
	_advance_fixed_steps(screen, 1)
	await get_tree().process_frame
	assert_int(selector.get_instance_id()).is_equal(selector_id)
	assert_bool(selector.has_focus()).is_true()
	assert_str(workspace.get_meta("selected_target_system", "")).is_equal("sensors")
	assert_str(screen.get_meta("last_fire_outcome", "")).is_empty()
	assert_bool(selector.focus_next.is_empty()).is_false()
	var fire := _find_action_button(screen, "fire-phasers") as Button
	assert_bool(fire.disabled).is_false()
	fire.grab_focus()
	fire.emit_signal("pressed")
	await get_tree().process_frame
	assert_str(screen.get_meta("last_fire_outcome", "")).is_equal("Accepted")
	assert_int(screen.get_meta("combat_remaining_cooldown", -1)).is_equal(2000)
	assert_bool(fire.disabled).is_true()
	assert_bool(fire.has_focus()).is_false()
	assert_object(get_viewport().gui_get_focus_owner()).is_not_null()
	assert_str(_collect_control_text(workspace)).contains("cooldown is active")
	var log_text := _collect_control_text(screen.get_node("%EventLogContent"))
	assert_str(log_text).contains("shot fired")
	assert_str(log_text).contains("shield impact")
	var ready_at: int = screen.get_meta("combat_next_ready_at", -1)
	fire.emit_signal("pressed")
	assert_int(screen.get_meta("combat_next_ready_at", -1)).is_equal(ready_at)
	_advance_fixed_steps(screen, 1)
	assert_float(screen.get_meta(_installation_meta("condition", SHIELDS_ID), 1.0)).is_less(1.0)
	assert_str(_collect_control_text(screen.get_node("%EventLogContent"))).contains("Own Shields damaged")
	screen.call("ShowEngineeringWorkspace")
	var repair := _find_engineering_action_button(screen, "repair:4") as Button
	assert_bool(repair.disabled).is_false()
	repair.emit_signal("pressed")
	assert_str(screen.get_meta("engineering_repair_target", "")).is_equal("shields")
	assert_str((screen.get_node("%Message") as Label).text).is_equal("Shield repair started.")
	screen.call("ShowTacticalView")
	assert_str(workspace.get_meta("selected_target_system", "")).is_equal("sensors")
	_advance_fixed_steps(screen, 19)
	assert_int(screen.get_meta("combat_remaining_cooldown", -1)).is_equal(0)
	assert_bool(fire.disabled).is_false()
	var live_text := _collect_control_text(screen)
	for forbidden in ["targetShipId", "controller", "target condition", "target capability", "Faction A", "pending stimulus"]:
		assert_str(live_text).not_contains(forbidden)


func _prepare_combat_encounter(screen: Node) -> void:
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	(_find_action_button(screen, "active-scan") as Button).emit_signal("pressed")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_str(screen.get_meta("first_contact_identification", "")).is_equal("Identified")
	assert_float(screen.get_meta("combat_known_range", -1.0)).is_greater(20.0)
	assert_str(screen.get_meta("combat_target_outcome", "")).is_equal("WeaponUnpowered")
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()
	(_find_action_button(screen, "hail") as Button).emit_signal("pressed")
	(screen.get_node("%CourseHeading") as SpinBox).value = 90
	(screen.get_node("%CourseSpeed") as SpinBox).value = 1
	(screen.get_node("%CourseButton") as Button).emit_signal("pressed")
	_advance_fixed_steps(screen, 120)
	assert_str(screen.get_meta("combat_target_outcome", "")).is_equal("WeaponUnpowered")
	(screen.get_node("%StopCourseButton") as Button).emit_signal("pressed")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal(90.0)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal(0.0)
	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "balance") as Button).emit_signal("pressed")
	screen.call("ShowTacticalView")
	assert_str(screen.get_meta("first_contact_status", "")).is_equal("Current")
	assert_str(screen.get_meta("combat_target_outcome", "")).is_equal("Accepted")


func test_public_combat_damage_disables_weapon_and_weapon_repair_restores_capability() -> void:
	var screen := _create_screen()
	_prepare_combat_encounter(screen)
	var selector := _command_deck(screen).get_node("%TargetSystemSelector") as OptionButton
	for index in range(selector.item_count):
		if selector.get_item_text(index) == "Sensors":
			selector.select(index)
			selector.emit_signal("item_selected", index)
	for shot in range(12):
		var fire := _find_action_button(screen, "fire-phasers") as Button
		if fire.disabled:
			break
		fire.emit_signal("pressed")
		_advance_fixed_steps(screen, 20)
	assert_float(screen.get_meta(_installation_meta("condition", WEAPONS_ID), -1.0)).is_equal(0.0)
	assert_str(screen.get_meta("combat_target_outcome", "")).is_equal("WeaponOffline")
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()
	assert_str(_collect_control_text(_command_deck(screen))).contains("Weapon is offline")
	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "repair:5") as Button).emit_signal("pressed")
	assert_str(screen.get_meta("engineering_repair_target", "")).is_equal("directed-energy-weapons")
	assert_str((screen.get_node("%Message") as Label).text).is_equal("Directed-energy weapon repair started.")
	_advance_fixed_steps(screen, 60)
	assert_float(screen.get_meta(_installation_meta("condition", WEAPONS_ID), -1.0)).is_equal(1.0)
	assert_str(screen.get_meta("engineering_repair_target", "")).is_empty()
	(_find_engineering_action_button(screen, "prioritize:5") as Button).emit_signal("pressed")
	assert_int(screen.get_meta(_installation_meta("allocation", WEAPONS_ID), -1)).is_equal(30)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(0)
	(_find_engineering_action_button(screen, "prioritize:4") as Button).emit_signal("pressed")
	assert_int(screen.get_meta(_installation_meta("allocation", SHIELDS_ID), -1)).is_equal(40)


func test_combat_quick_save_restores_readiness_and_reaction_continuation() -> void:
	var screen := _create_screen()
	_prepare_combat_encounter(screen)
	(_find_action_button(screen, "fire-phasers") as Button).emit_signal("pressed")
	assert_str(screen.get_meta("last_fire_outcome", "")).is_equal("Accepted")
	var ready_at: int = screen.get_meta("combat_next_ready_at", -1)
	var fired_at: int = screen.get_meta("simulation_time_milliseconds", -1)
	assert_int(ready_at).is_equal(fired_at + 2000)
	screen.call("QuickSave")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("saved")
	var saved_text := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var saved: Dictionary = JSON.parse_string(saved_text)
	var simulation: Dictionary = saved.get("simulation", {})
	var pending_count := 0
	for ship in simulation.get("ships", []):
		var combat: Dictionary = ship.get("combat", {})
		var stimulus = combat.get("pendingStimulus")
		if stimulus != null:
			pending_count += 1
			assert_int(int(stimulus.get("observedAtMilliseconds", -1))).is_equal(fired_at)
			assert_int(int(stimulus.get("dueTimeMilliseconds", -1))).is_equal(fired_at + 100)
	assert_int(pending_count).is_equal(1)
	_advance_fixed_steps(screen, 20)
	var shield_condition: float = screen.get_meta(_installation_meta("condition", SHIELDS_ID), -1.0)
	var weapon_condition: float = screen.get_meta(_installation_meta("condition", WEAPONS_ID), -1.0)
	assert_float(shield_condition).is_less(1.0)
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	screen.call("SelectContact", 1)
	assert_int(screen.get_meta("combat_next_ready_at", -1)).is_equal(ready_at)
	assert_int(screen.get_meta("combat_remaining_cooldown", -1)).is_equal(2000)
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()
	screen.call("QuickSave")
	var loaded: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH))
	assert_dict(loaded.get("simulation", {})).is_equal(simulation)
	_advance_fixed_steps(screen, 20)
	assert_int(screen.get_meta("combat_remaining_cooldown", -1)).is_equal(0)
	assert_float(screen.get_meta(_installation_meta("condition", SHIELDS_ID), -1.0)).is_equal_approx(shield_condition, 0.000001)
	assert_float(screen.get_meta(_installation_meta("condition", WEAPONS_ID), -1.0)).is_equal_approx(weapon_condition, 0.000001)
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_false()


func test_core_out_of_range_reason_uses_legitimate_contact_and_player_only_allocation_fixture() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	(_find_action_button(screen, "active-scan") as Button).emit_signal("pressed")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_str(screen.get_meta("first_contact_identification", "")).is_equal("Identified")
	assert_float(screen.get_meta("combat_known_range", -1.0)).is_greater(20.0)
	(_find_action_button(screen, "hail") as Button).emit_signal("pressed")
	(screen.get_node("%StopCourseButton") as Button).emit_signal("pressed")
	screen.call("QuickSave")
	var save_text := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var parsed: Dictionary = JSON.parse_string(save_text)
	assert_int(int(parsed.get("schemaVersion", -1))).is_equal(10)
	var simulation: Dictionary = parsed.get("simulation", {})
	var player_id := int(simulation.get("playerShipId", 0))
	# Presets cannot jointly power weapons and sense beyond 20 km with 75 units. Only own allocation
	# changes in this fixture; scan acquired the Current/Identified contact through ordinary controls.
	# V10 installations are ship-local; the player (ship 1) is written first, so the first installedSystems array
	# is the player's. Production installed ids: 3 = impulse, 5 = directed-energy weapons.
	assert_int(player_id).is_equal(1)
	save_text = _replace_saved_object_field(save_text, "installedSystems", "installedSystemId", 3, "allocation", 5, 0)
	save_text = _replace_saved_object_field(save_text, "installedSystems", "installedSystemId", 5, "allocation", 0, 5)
	_write_text(TEST_QUICK_SAVE_PATH, save_text)
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	screen.call("SelectContact", 1)
	assert_str(screen.get_meta("first_contact_status", "")).is_equal("Current")
	assert_str(screen.get_meta("first_contact_identification", "")).is_equal("Identified")
	assert_str(screen.get_meta("combat_target_outcome", "")).is_equal("OutOfRange")
	var fire := _find_action_button(screen, "fire-phasers") as Button
	assert_bool(fire.disabled).is_true()
	assert_str(fire.tooltip_text).contains("out of weapon range")
	assert_str(_collect_control_text(_command_deck(screen))).contains("out of weapon range")
	var ready_at: int = screen.get_meta("combat_next_ready_at", -1)
	fire.emit_signal("pressed")
	assert_int(screen.get_meta("combat_next_ready_at", -1)).is_equal(ready_at)


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
	assert_str((screen.get_node("%ViewStatus") as Label).text).is_equal("COMMAND DECK / TRAVEL")


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
	var focus_style := screen.theme.get_stylebox("focus", "Button") as StyleBoxFlat
	var normal_style := screen.theme.get_stylebox("normal", "Button") as StyleBoxFlat
	assert_bool(focus_style.border_color.is_equal_approx(Color("67c6d4"))).is_true()
	assert_int(focus_style.border_width_left).is_equal(2)
	assert_int(normal_style.border_width_left).is_equal(1)


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
	var engineering_focus := get_viewport().gui_get_focus_owner()
	assert_object(engineering_focus).is_not_null()
	assert_bool(_engineering_workspace(screen).is_ancestor_of(engineering_focus)).is_true()
	var reached_command := false
	var reached_bottom_return := false
	var cycled_to_entry := false
	var current := engineering_focus
	for _step in range(64):
		assert_str(str(current.focus_next)).is_not_empty()
		var next := current.get_node_or_null(current.focus_next) as Control
		assert_object(next).is_not_null()
		if next == null:
			break
		assert_bool(next.is_visible_in_tree()).is_true()
		var previous := next.get_node_or_null(next.focus_previous) as Control
		assert_object(previous).is_not_null()
		assert_bool(previous == current).is_true()
		reached_command = reached_command or next == screen.get_node("%CommandStationButton")
		reached_bottom_return = reached_bottom_return or next == screen.get_node(
			"%EngineeringBottomReturnButton"
		)
		current = next
		if current == engineering_focus:
			cycled_to_entry = true
			break
	assert_bool(reached_command).is_true()
	assert_bool(reached_bottom_return).is_true()
	assert_bool(cycled_to_entry).is_true()
	assert_bool((_engineering_workspace(screen) as Control).visible).is_true()
	assert_bool((_command_deck(screen) as Control).visible).is_false()
	screen.call("ShowCommandWorkspace")
	await get_tree().process_frame
	assert_bool(command.has_focus()).is_true()


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


func test_engineering_minimums_and_key_panels_remain_accessible_at_1600_by_900() -> void:
	var screen := _create_screen()
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = Vector2(1600, 900)
	screen.call("ShowPreview", 3)
	await get_tree().process_frame

	var engineering := _engineering_workspace(screen) as Control
	var workspace_margin := engineering.get_node("WorkspaceMargin") as MarginContainer
	var workspace_stack := engineering.get_node("WorkspaceMargin/WorkspaceStack") as VBoxContainer
	var allocation := engineering.get_node("%PowerAllocationSummary") as Control
	var inspector_scroll := engineering.get_node("%EngineeringInspectorScroll") as ScrollContainer
	var actions := engineering.get_node("%EngineeringActions") as Control
	assert_float(workspace_stack.get_combined_minimum_size().y).is_less_equal(
		workspace_margin.size.y
	)
	assert_float(allocation.get_global_rect().end.y).is_less_equal(
		engineering.get_global_rect().end.y
	)
	assert_bool(inspector_scroll.is_ancestor_of(actions)).is_true()
	assert_bool(
		inspector_scroll.get_v_scroll_bar().max_value
		> inspector_scroll.get_v_scroll_bar().page
	).is_true()


func test_engineering_uses_native_schematic_projection() -> void:
	var screen := _create_screen()
	screen.call("ShowPreview", 3)
	await get_tree().process_frame

	var schematic := _engineering_workspace(screen).get_node("%EngineeringSchematic") as Control
	assert_object(schematic).is_not_null()
	assert_str((screen.get_node("%ViewStatus") as Label).text).is_equal("ENGINEERING WORKSPACE")
	assert_str(schematic.get_script().resource_path).ends_with("EngineeringSchematicView.cs")
	assert_int(schematic.get_meta("component_count", 0)).is_equal(9)
	assert_int(schematic.get_meta("link_count", 0)).is_equal(7)
	assert_bool(schematic.get_meta("is_preview", false)).is_true()
	assert_float(schematic.get_meta("maximum_component_width", 0.0)).is_equal(220.0)
	assert_bool(schematic.get_meta("topology_available", false)).is_true()
	assert_object(_engineering_workspace(screen).get_node_or_null("%SchematicComponents")).is_null()
	assert_object(_engineering_workspace(screen).get_node_or_null("%SchematicLinks")).is_null()


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


func test_engineering_reuses_persistent_bottom_area_and_return_restores_command_mode() -> void:
	var screen := _create_screen()
	var captain_actions := screen.get_node("%CaptainActions") as Control
	var engineering_actions := screen.get_node("%EngineeringBottomActions") as Control

	assert_str(screen.get_meta("bottom_area_mode", "")).is_equal("command")
	assert_str((screen.get_node("%EventLogHeading") as Label).text).is_equal("EVENT / ORDER LOG")
	assert_bool(captain_actions.visible).is_true()
	assert_bool(engineering_actions.visible).is_false()
	assert_object(
		_engineering_workspace(screen).get_node_or_null(
			"WorkspaceMargin/WorkspaceStack/ActivityRegion"
		)
	).is_null()

	screen.call("ShowPreview", 3)
	assert_str(screen.get_meta("bottom_area_mode", "")).is_equal("engineering")
	assert_str((screen.get_node("%EventLogHeading") as Label).text).is_equal(
		"ENGINEERING EVENT LOG"
	)
	assert_bool(captain_actions.visible).is_false()
	assert_bool(engineering_actions.visible).is_true()
	assert_str(_collect_control_text(screen.get_node("%EngineeringQueueContent"))).contains(
		"EPS Bus A-4 inspection"
	)
	var preview_action := screen.get_node("%EngineeringQueueActions").get_node(
		"BottomAction_reorder_repairs"
	) as Button
	assert_bool(preview_action.disabled).is_true()
	assert_str(preview_action.text).contains("PREVIEW ONLY")

	(screen.get_node("%EngineeringBottomReturnButton") as Button).emit_signal("pressed")
	assert_str(screen.get_meta("active_workspace", "")).is_equal("command")
	assert_str(screen.get_meta("bottom_area_mode", "")).is_equal("command")
	assert_bool(captain_actions.visible).is_true()
	assert_bool(engineering_actions.visible).is_false()


func test_command_inspector_keeps_mode_sections_above_quick_actions() -> void:
	var screen := _create_screen()

	screen.call("ShowPreview", 1)
	assert_str((_command_deck(screen).get_node("%InspectorHeading") as Label).text).is_equal(
		"DESTINATION / ROUTE"
	)
	# Destination, route, and the travel preview's own last-known-contacts fixture section.
	assert_int(_command_deck(screen).get_node("%InspectorContent").get_child_count()).is_equal(3)
	assert_object(_command_deck(screen).get_node_or_null("%ContextActions")).is_instanceof(
		VBoxContainer
	)

	screen.call("ShowPreview", 2)
	assert_str((screen.get_node("%ViewStatus") as Label).text).is_equal("COMMAND DECK / COMBAT")
	assert_str((_command_deck(screen).get_node("%InspectorHeading") as Label).text).is_equal(
		"SELECTED CONTACT / TACTICAL SUMMARY"
	)
	assert_int(_command_deck(screen).get_node("%InspectorContent").get_child_count()).is_equal(2)
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()


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


func test_live_context_action_identity_and_focus_survive_projection_refresh() -> void:
	var screen := _create_screen()
	screen.call("ShowTacticalView")
	await get_tree().process_frame
	var course_button := _find_action_button(screen, "set-tactical-course") as Button
	var advance_button := _find_action_button(screen, "advance-time") as Button
	var course_instance_id := course_button.get_instance_id()
	var advance_instance_id := advance_button.get_instance_id()
	course_button.grab_focus()
	assert_bool(course_button.has_focus()).is_true()

	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	await get_tree().process_frame

	var refreshed_course := _find_action_button(screen, "set-tactical-course") as Button
	var refreshed_advance := _find_action_button(screen, "advance-time") as Button
	assert_int(refreshed_course.get_instance_id()).is_equal(course_instance_id)
	assert_int(refreshed_advance.get_instance_id()).is_equal(advance_instance_id)
	assert_bool(refreshed_course.has_focus()).is_true()
	refreshed_advance.emit_signal("pressed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(8000)


func test_live_engineering_projects_authoritative_power_capability_and_repair_state() -> void:
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	var engineering := _engineering_workspace(screen)
	var presented := _collect_control_text(engineering)

	assert_str(presented).contains("OVERVIEW")
	assert_str(presented).contains("POWER")
	assert_str(presented).contains("SENSORS")
	assert_str(presented).contains("PROPULSION")
	assert_str(presented).contains("REPAIRS")
	assert_str(presented).contains("NOMINAL GENERATION")
	assert_str(presented).contains("120 units")
	assert_str(presented).contains("AVAILABLE POWER")
	assert_str(presented).contains("75 units")
	assert_str(presented).contains("ENGINEERING CONTROLS")
	assert_str(presented).contains("MAX TACTICAL SPEED")
	(engineering.get_node("%EngineeringHierarchy").get_node("Hierarchy_system_2") as Button).emit_signal(
		"pressed"
	)
	var sensor_inspector := _collect_control_text(engineering.get_node("%ComponentInspectorContent"))
	assert_str(sensor_inspector).contains("CAPABILITY")
	assert_str(sensor_inspector).contains("PASSIVE RANGE")
	(engineering.get_node("%EngineeringHierarchy").get_node("Hierarchy_repairs") as Button).emit_signal(
		"pressed"
	)
	var repair_inspector := _collect_control_text(engineering.get_node("%ComponentInspectorContent"))
	assert_str(repair_inspector).contains("Sensors")
	assert_str(repair_inspector).contains("8.0 s")
	assert_bool((engineering.get_node("%EngineeringTabs") as Control).visible).is_false()
	assert_str((engineering.get_node("%SchematicHeading") as Label).text).is_equal(
		"ENGINEERING CAPABILITY"
	)
	var hierarchy_text := _collect_control_text(engineering.get_node("%EngineeringHierarchy"))
	for unsupported in ["EPS", "BATTERIES", "WARP CORE", "LIFE SUPPORT"]:
		assert_str(hierarchy_text).not_contains(unsupported)
	assert_str(hierarchy_text).contains("SHIELDS")
	assert_str(hierarchy_text).contains("DIRECTED-ENERGY WEAPONS")

	assert_int(screen.get_meta("engineering_nominal_power", -1)).is_equal(120)
	assert_int(screen.get_meta("engineering_available_power", -1)).is_equal(75)
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(44)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(31)
	assert_str(screen.get_meta("engineering_repair_target", "")).is_equal("sensors")
	assert_bool((_find_engineering_action_button(screen, "balance") as Button).disabled).is_false()
	assert_bool((_find_engineering_action_button(screen, "repair:2") as Button).disabled).is_true()
	assert_str(
		(_find_engineering_action_button(screen, "repair:2") as Button).tooltip_text
	).contains("another system repair is active")


func test_live_engineering_allocation_presets_submit_core_choices_and_refresh_projection() -> void:
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")

	(_find_engineering_action_button(screen, "prioritize:2") as Button).emit_signal("pressed")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(70)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(5)
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal(
		"Prioritize:2:Accepted"
	)
	assert_str((screen.get_node("%Message") as Label).text).contains("Sensor-priority allocation applied")

	(_find_engineering_action_button(screen, "prioritize:3") as Button).emit_signal("pressed")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(25)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(50)
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal(
		"Prioritize:3:Accepted"
	)

	(_find_engineering_action_button(screen, "balance") as Button).emit_signal("pressed")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(28)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(20)
	assert_int(screen.get_meta(_installation_meta("allocation", SHIELDS_ID), -1)).is_equal(16)
	assert_int(screen.get_meta(_installation_meta("allocation", WEAPONS_ID), -1)).is_equal(11)
	assert_int(screen.get_meta("engineering_reserve", -1)).is_equal(0)
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal(
		"Balance:-:Accepted"
	)


func test_live_engineering_rows_actions_and_labels_reproduce_base_presentation() -> void:
	# Pins every production Engineering string and the button order of the pre-substrate UI (#121): rows and
	# buttons now come from Core's installed-system projection keyed by installed id, and the visible text from
	# the Godot presentation table, so drift in either the projection order or the table fails here.
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	var engineering := _engineering_workspace(screen)
	assert_array(_button_texts(engineering.get_node("%EngineeringHierarchy"))).is_equal([
		"Hierarchy_overview=OVERVIEW",
		"Hierarchy_system_1=POWER",
		"Hierarchy_system_2=SENSORS",
		"Hierarchy_system_3=PROPULSION",
		"Hierarchy_system_4=SHIELDS",
		"Hierarchy_system_5=DIRECTED-ENERGY WEAPONS",
		"Hierarchy_repairs=REPAIRS  [1 !]",
	])
	# The production start already runs the sensor repair, so every repair button is disabled.
	assert_array(_button_texts(engineering.get_node("%EngineeringActionsContent"))).is_equal([
		"Action_balance=Balance power allocation",
		"Action_prioritize_2=Prioritize sensors",
		"Action_prioritize_3=Prioritize propulsion",
		"Action_prioritize_4=Prioritize shields",
		"Action_prioritize_5=Prioritize weapons",
		"Action_repair_4=Begin shield repair  [UNAVAILABLE]",
		"Action_repair_5=Begin weapon repair  [UNAVAILABLE]",
		"Action_repair_2=Begin sensor repair  [UNAVAILABLE]",
		"Action_repair_3=Begin impulse repair  [UNAVAILABLE]",
		"Action_return_command=Return to Command Deck",
	])
	assert_array(_section_rows(engineering.get_node("%ConnectedLoadsContent"))).is_equal([
		"CONNECTED LOADS",
		"SENSORS=44 units",
		"IMPULSE PROPULSION=31 units",
		"SHIELDS=0 units",
		"DIRECTED-ENERGY WEAPONS=0 units",
	])
	assert_array(_section_rows(engineering.get_node("%PowerAllocationContent"))).is_equal([
		"POWER ALLOCATION SUMMARY",
		"NOMINAL GENERATION=120 units",
		"AVAILABLE POWER=75 units",
		"SENSORS=44 units",
		"PROPULSION=31 units",
		"SHIELDS=0 units",
		"DIRECTED-ENERGY WEAPONS=0 units",
		"RESERVE=0 units",
	])
	var expected_sections := {
		"overview": ["ENGINEERING OVERVIEW", "AVAILABLE POWER", "RESERVE", "SENSOR RANGE", "MAX TACTICAL SPEED"],
		"system_1": ["POWER GENERATION", "NOMINAL", "AVAILABLE", "CONDITION", "RESERVE"],
		"system_2": ["SENSORS", "CONDITION", "ALLOCATION", "CAPABILITY", "PASSIVE RANGE"],
		"system_3": ["IMPULSE PROPULSION", "CONDITION", "ALLOCATION", "CAPABILITY", "MAX TACTICAL SPEED"],
		"system_4": ["SHIELDS", "CONDITION", "ALLOCATION", "DEMAND", "CAPABILITY"],
		"system_5": ["DIRECTED-ENERGY WEAPONS", "CONDITION", "ALLOCATION", "DEMAND", "CAPABILITY"],
		"repairs": ["ACTIVE REPAIR", "TARGET", "PROGRESS", "COMPLETION"],
	}
	var inspector := engineering.get_node("%ComponentInspectorContent")
	for row_id in expected_sections:
		(engineering.get_node("%EngineeringHierarchy").get_node("Hierarchy_" + row_id) as Button).emit_signal(
			"pressed"
		)
		var labels: Array[String] = []
		for row in _section_rows(inspector):
			labels.append(row.get_slice("=", 0))
		assert_array(labels).is_equal(expected_sections[row_id])
	assert_array(_section_rows(inspector)).contains(["TARGET=Sensors"])

	var message := screen.get_node("%Message") as Label
	var accepted := {
		"prioritize:2": "Sensor-priority allocation applied.",
		"prioritize:3": "Propulsion-priority allocation applied.",
		"prioritize:4": "Shield-priority allocation applied.",
		"prioritize:5": "Weapon-priority allocation applied.",
		"balance": "Balanced allocation applied.",
	}
	for action_id in accepted:
		(_find_engineering_action_button(screen, action_id) as Button).emit_signal("pressed")
		assert_str(message.text).is_equal(accepted[action_id])
	# Refusals name the offending consumer by installed id (2 sensors, 3 impulse); shields and weapons never had a
	# dedicated message, so they keep the base generic refusal.
	assert_str(screen.call("DescribeDemandRefusal", 2)).is_equal(
		"Allocation unavailable: sensor demand would be exceeded."
	)
	assert_str(screen.call("DescribeDemandRefusal", 3)).is_equal(
		"Allocation unavailable: propulsion demand would be exceeded."
	)
	for silent_id in [4, 5]:
		assert_str(screen.call("DescribeDemandRefusal", silent_id)).is_equal("Power allocation was not accepted.")


func test_stale_engineering_control_is_refused_after_quick_load_but_acts_after_refresh() -> void:
	# Codex disposition 1 / risk R6: installed ids are ship-local, so a control presented before a load must not
	# act afterwards even though the loaded game exposes the identical "prioritize:3" key. The positive control
	# shows the same kind of captured button still acting across an ordinary refresh.
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	screen.call("QuickSave")
	var refreshed := _find_engineering_action_button(screen, "prioritize:2") as Button
	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	assert_bool(_find_engineering_action_button(screen, "prioritize:2") == refreshed).is_true()
	refreshed.emit_signal("pressed")
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal("Prioritize:2:Accepted")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(70)

	var stale := _find_engineering_action_button(screen, "prioritize:3") as Button
	stale.grab_focus()
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	var current := _find_engineering_action_button(screen, "prioritize:3") as Button
	assert_object(current).is_not_null()
	assert_bool(current == stale).is_false()
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(44)

	stale.emit_signal("pressed")
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal("Prioritize:2:Accepted")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(44)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(31)
	assert_str(screen.get_meta("last_refused_action", "")).is_equal("prioritize:3")
	assert_str((screen.get_node("%Message") as Label).text).contains("no longer available")
	# The stale button's queued focus restore is dropped; the load's own focus request wins.
	await get_tree().process_frame
	assert_object(get_viewport().gui_get_focus_owner()).is_same(screen.get_node("%EngineeringStationButton"))

	current.emit_signal("pressed")
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal("Prioritize:3:Accepted")
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(50)


func test_stale_command_deck_control_is_refused_after_quick_load() -> void:
	# The Command Deck binds its controls the same way; the refresh-survival positive control is
	# test_live_context_action_identity_and_focus_survive_projection_refresh.
	var screen := _create_screen()
	screen.call("ShowTacticalView")
	screen.call("QuickSave")
	var stale := _find_action_button(screen, "advance-time") as Button
	var time_before: int = screen.get_meta("simulation_time_milliseconds", -1)
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_bool(_find_action_button(screen, "advance-time") == stale).is_false()

	stale.emit_signal("pressed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(time_before)
	assert_str(screen.get_meta("last_refused_action", "")).is_equal("advance-time")
	assert_str((screen.get_node("%Message") as Label).text).contains("no longer available")
	(_find_action_button(screen, "advance-time") as Button).emit_signal("pressed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_greater(time_before)


func test_live_quick_load_round_trips_v10_installations_into_engineering() -> void:
	# Design §7.6 live-game regression: a V10 quick-save restores the saved installations, and the Engineering
	# projection shown afterwards is the loaded one, not the pre-load live state.
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	screen.call("QuickSave")
	var saved := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	assert_int(int((JSON.parse_string(saved) as Dictionary).get("schemaVersion", -1))).is_equal(10)
	(_find_engineering_action_button(screen, "prioritize:2") as Button).emit_signal("pressed")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(70)

	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_str(screen.get_meta("engineering_system_ids", "")).is_equal("1,2,3,4,5")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(44)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(31)
	assert_str(screen.get_meta("engineering_repair_target_id", "")).is_equal("2")
	assert_array(_section_rows(_engineering_workspace(screen).get_node("%ConnectedLoadsContent"))).contains(
		["SENSORS=44 units"]
	)
	screen.call("QuickSave")
	var resaved: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH))
	assert_dict(resaved.get("simulation", {})).is_equal((JSON.parse_string(saved) as Dictionary).get("simulation", {}))


func test_quick_load_without_player_shields_presents_only_actual_installations() -> void:
	# Codex disposition 5: remove only the player's installation 4. The pathfinder.shields definition row stays
	# because the NPC ships still reference it, so the load must succeed before absence is asserted.
	var screen := _create_screen()
	screen.call("QuickSave")
	var saved := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var edited := _regex_replace_first(saved, ',\\s*\\{\\s*"installedSystemId"\\s*:\\s*4\\s*,[^}]*\\}', "")
	assert_int(edited.count("pathfinder.shields")).is_equal(saved.count("pathfinder.shields") - 1)
	_write_text(TEST_QUICK_SAVE_PATH, edited)

	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(0)
	assert_str(screen.get_meta("engineering_system_ids", "")).is_equal("1,2,3,5")
	assert_bool(screen.has_meta(_installation_meta("condition", SHIELDS_ID))).is_false()
	var deck_shields := _command_deck(screen).get_node("%SystemRows").get_node("System_shields")
	assert_str((deck_shields.get_child(1) as Label).text).is_equal("UNAVAILABLE")

	screen.call("ShowEngineeringWorkspace")
	var engineering := _engineering_workspace(screen)
	assert_array(_button_texts(engineering.get_node("%EngineeringHierarchy"))).is_equal([
		"Hierarchy_overview=OVERVIEW",
		"Hierarchy_system_1=POWER",
		"Hierarchy_system_2=SENSORS",
		"Hierarchy_system_3=PROPULSION",
		"Hierarchy_system_5=DIRECTED-ENERGY WEAPONS",
		"Hierarchy_repairs=REPAIRS  [1 !]",
	])
	assert_object(_find_engineering_action_button(screen, "prioritize:4")).is_null()
	assert_object(_find_engineering_action_button(screen, "repair:4")).is_null()
	assert_array(_section_rows(engineering.get_node("%ConnectedLoadsContent"))).is_equal([
		"CONNECTED LOADS",
		"SENSORS=44 units",
		"IMPULSE PROPULSION=31 units",
		"DIRECTED-ENERGY WEAPONS=0 units",
	])

	screen.call("ShowCommandWorkspace")
	screen.call("ShowTacticalView")
	var combat_text := _collect_control_text(_command_deck(screen))
	assert_str(combat_text).contains("SHIELD CONDITION")
	assert_str(combat_text).contains("WEAPON CONDITION")
	assert_int(combat_text.count("UNAVAILABLE")).is_greater(0)


func test_quick_load_rejects_save_missing_a_still_referenced_definition() -> void:
	# The malformed counterpart of the absence fixture: deleting the definition row while ships still reference it
	# must refuse the load and leave the live game and its presentation untouched.
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	screen.call("QuickSave")
	var saved := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var row := RegEx.new()
	assert_int(
		row.compile(',?\\s*\\{\\s*"definitionId"\\s*:\\s*"pathfinder\\.shields"\\s*,\\s*"semantics"\\s*:\\s*"[^"]*"\\s*\\}')
	).is_equal(OK)
	var removed := row.search(saved)
	assert_object(removed).is_not_null()
	# The row is last in the sorted definition list, so removing its leading comma keeps the JSON well formed.
	assert_str(removed.get_string().strip_edges().left(1)).is_equal(",")
	_write_text(TEST_QUICK_SAVE_PATH, row.sub(saved, "", false))
	(_find_engineering_action_button(screen, "prioritize:2") as Button).emit_signal("pressed")
	var identity: int = screen.get_meta("simulation_identity", 0)

	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("load_failed")
	assert_int(screen.get_meta("simulation_identity", 0)).is_equal(identity)
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(70)
	assert_str(screen.get_meta("engineering_system_ids", "")).is_equal("1,2,3,4,5")
	assert_object(_find_engineering_action_button(screen, "prioritize:4")).is_not_null()


func test_stale_engineering_control_is_refused_after_load_changes_the_owner() -> void:
	# Owner half of Codex disposition 1: the loaded player ship has a different instance id, yet exposes the
	# identical "prioritize:4" key. The captured control must not act; the freshly presented one does.
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	screen.call("QuickSave")
	_write_text(TEST_QUICK_SAVE_PATH, _shift_ship_ids(FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH), 10))
	var stale := _find_engineering_action_button(screen, "prioritize:4") as Button
	assert_int(screen.get_meta("player_ship_id", -1)).is_equal(1)

	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("player_ship_id", -1)).is_equal(11)
	var current := _find_engineering_action_button(screen, "prioritize:4") as Button
	assert_bool(current == stale).is_false()
	stale.emit_signal("pressed")
	assert_str(screen.get_meta("last_refused_action", "")).is_equal("prioritize:4")
	assert_int(screen.get_meta(_installation_meta("allocation", SHIELDS_ID), -1)).is_equal(0)
	assert_str(screen.get_meta("last_engineering_command", "")).is_empty()
	current.emit_signal("pressed")
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal("Prioritize:4:Accepted")
	assert_int(screen.get_meta(_installation_meta("allocation", SHIELDS_ID), -1)).is_equal(40)


func test_stale_engineering_control_is_refused_after_load_changes_installation_meaning() -> void:
	# Meaning half of Codex disposition 1: in the loaded save installation 4 is the weapon and 5 the shield, so
	# "prioritize:4" now names a different system. The captured shield control must not prioritize the weapon.
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	screen.call("QuickSave")
	var saved := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var edited := _regex_replace_first(
		saved,
		'"installedSystemId"(\\s*):(\\s*)4(\\s*),(\\s*)"definitionId"(\\s*):(\\s*)"pathfinder\\.shields"',
		'"installedSystemId"$1:${2}4$3,$4"definitionId"$5:$6"pathfinder.directed-energy-weapons"'
	)
	edited = _regex_replace_first(
		edited,
		'"installedSystemId"(\\s*):(\\s*)5(\\s*),(\\s*)"definitionId"(\\s*):(\\s*)"pathfinder\\.directed-energy-weapons"',
		'"installedSystemId"$1:${2}5$3,$4"definitionId"$5:$6"pathfinder.shields"'
	)
	# Weapon readiness belongs to the installed weapon, which is now installation 4.
	edited = _regex_replace_first(edited, '"weaponInstalledSystemId"(\\s*):(\\s*)5', '"weaponInstalledSystemId"$1:${2}4')
	_write_text(TEST_QUICK_SAVE_PATH, edited)
	var stale := _find_engineering_action_button(screen, "prioritize:4") as Button
	assert_str(stale.text).is_equal("Prioritize shields")

	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	var current := _find_engineering_action_button(screen, "prioritize:4") as Button
	assert_bool(current == stale).is_false()
	assert_str(current.text).is_equal("Prioritize weapons")
	# Prioritize actions follow Core's common order: shields (now 5) before weapons (now 4).
	var actions := _button_texts(_engineering_workspace(screen).get_node("%EngineeringActionsContent"))
	assert_int(actions.find("Action_prioritize_5=Prioritize shields")).is_less(
		actions.find("Action_prioritize_4=Prioritize weapons")
	)
	stale.emit_signal("pressed")
	assert_str(screen.get_meta("last_refused_action", "")).is_equal("prioritize:4")
	# In this save installation 4 is the weapon, so the canonical WEAPONS_ID mapping does not apply.
	assert_int(screen.get_meta(_installation_meta("allocation", 4), -1)).is_equal(0)
	current.emit_signal("pressed")
	assert_str((screen.get_node("%Message") as Label).text).is_equal("Weapon-priority allocation applied.")
	assert_int(screen.get_meta(_installation_meta("allocation", 4), -1)).is_equal(30)


func test_binding_owner_check_refuses_a_different_ship_under_the_current_generation() -> void:
	# Review F2: every quick-load bumps the generation, so the load fixtures above can never isolate the owner half
	# of the binding check. The hook runs the production comparison with each half varied on its own.
	var screen := _create_screen()
	var generation: int = screen.get_meta("simulation_generation", -1)
	var owner: int = screen.get_meta("player_ship_id", -1)
	assert_int(generation).is_greater(0)
	assert_bool(screen.call("IsBindingCurrent", owner, generation)).is_true()
	assert_bool(screen.call("IsBindingCurrent", owner + 10, generation)).is_false()
	assert_bool(screen.call("IsBindingCurrent", owner, generation - 1)).is_false()


func test_pre_load_engineering_selection_and_focus_do_not_apply_after_owner_changing_load() -> void:
	# Review F1: hierarchy rows are keyed "system:<id>" and installed ids are ship-local, so after a load that
	# changes the player ship a selection, row button, or queued focus restore captured before it must not carry
	# over onto the loaded ship's installation with the same id. The same-ship reload that keeps a still-valid
	# selection is pinned by test_live_engineering_identity_selection_current_payload_and_traversal_survive_refresh.
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	screen.call("QuickSave")
	_write_text(TEST_QUICK_SAVE_PATH, _shift_ship_ids(FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH), 10))
	var engineering := _engineering_workspace(screen)
	var hierarchy := engineering.get_node("%EngineeringHierarchy")
	var stale := hierarchy.get_node("Hierarchy_system_4") as Button
	stale.emit_signal("pressed")
	stale.grab_focus()
	assert_str(engineering.get_meta("selected_component_id", "")).is_equal("system:4")
	# An ordinary refresh queues the workspace's deferred focus restore for the focused row; the load then
	# replaces the world before that deferred call flushes.
	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("player_ship_id", -1)).is_equal(11)

	assert_str(engineering.get_meta("selected_component_id", "")).is_equal("overview")
	var current := hierarchy.get_node_or_null("Hierarchy_system_4") as Button
	assert_object(current).is_not_null()
	assert_bool(current == stale).is_false()
	assert_bool(current.button_pressed).is_false()
	stale.emit_signal("pressed")
	assert_str(engineering.get_meta("selected_component_id", "")).is_equal("overview")
	await get_tree().process_frame
	assert_bool(is_instance_valid(stale) and stale.has_focus()).is_false()
	assert_object(get_viewport().gui_get_focus_owner()).is_same(screen.get_node("%EngineeringStationButton"))

	current.emit_signal("pressed")
	assert_str(engineering.get_meta("selected_component_id", "")).is_equal("system:4")


func test_course_draft_authored_before_owner_changing_load_is_rederived_from_loaded_ship() -> void:
	# Review F3: the course inputs are persistent shell controls, so their draft outlives a quick-load. A heading
	# and speed typed for the pre-load ship must not be submitted to the loaded player ship; the draft is
	# re-derived from the loaded projection, and the same persistent button keeps working afterwards.
	var screen := _create_screen()
	screen.call("ShowTacticalView")
	screen.call("QuickSave")
	_write_text(TEST_QUICK_SAVE_PATH, _shift_ship_ids(FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH), 10))
	var heading := screen.get_node("%CourseHeading") as SpinBox
	var speed := screen.get_node("%CourseSpeed") as SpinBox
	heading.value = 270
	speed.value = 3

	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("player_ship_id", -1)).is_equal(11)
	var loaded_heading: float = screen.get_meta("tactical_heading", -1.0)
	var loaded_speed: float = screen.get_meta("tactical_speed", -1.0)
	assert_float(heading.value).is_equal_approx(loaded_heading, 0.0001)
	assert_float(speed.value).is_equal_approx(loaded_speed, 0.0001)
	var course_button := screen.get_node("%CourseButton") as Button
	course_button.emit_signal("pressed")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(loaded_heading, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(loaded_speed, 0.0001)

	heading.value = 90
	speed.value = 1
	course_button.emit_signal("pressed")
	assert_float(screen.get_meta("tactical_heading", -1.0)).is_equal_approx(90.0, 0.0001)
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(1.0, 0.0001)


func test_engineering_telemetry_hooks_are_keyed_by_installed_identity() -> void:
	# Review F4: telemetry hooks name installations, not kinds, so they never pick "the" row of a kind; removing an
	# installation on load removes its hooks (the absence fixture covers that path).
	var screen := _create_screen()
	assert_str(screen.get_meta("engineering_system_ids", "")).is_equal("1,2,3,4,5")
	for installed_id in [1, 2, 3, 4, 5]:
		assert_bool(screen.has_meta(_installation_meta("condition", installed_id))).is_true()
	# Power generation is the supplier, not a consumer, so it has no allocation hook.
	assert_bool(screen.has_meta(_installation_meta("allocation", 1))).is_false()
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(44)
	assert_int(screen.get_meta(_installation_meta("allocation", IMPULSE_ID), -1)).is_equal(31)
	assert_int(screen.get_meta(_installation_meta("allocation", SHIELDS_ID), -1)).is_equal(0)
	assert_int(screen.get_meta(_installation_meta("allocation", WEAPONS_ID), -1)).is_equal(0)
	for kind_hook in [
		"engineering_sensor_allocation",
		"engineering_impulse_allocation",
		"engineering_shield_allocation",
		"engineering_weapon_allocation",
		"engineering_shield_condition",
		"engineering_weapon_condition",
		"engineering_sensor_capability",
		"engineering_impulse_capability",
	]:
		assert_bool(screen.has_meta(kind_hook)).is_false()


func test_sensor_priority_refreshes_command_contacts_without_revealing_hidden_identity() -> void:
	var screen := _create_screen()
	assert_int(screen.get_meta("sensor_contact_count", -1)).is_equal(0)
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_int(screen.get_meta("sensor_contact_count", -1)).is_equal(0)

	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "prioritize:2") as Button).emit_signal("pressed")
	assert_int(screen.get_meta("sensor_contact_count", -1)).is_equal(1)
	assert_str(screen.get_meta("first_contact_label", "")).is_equal("Contact 1")
	assert_str(screen.get_meta("first_contact_identification", "")).is_equal("Detected")
	screen.call("ShowTacticalView")
	assert_str(_collect_control_text(_command_deck(screen))).not_contains("Survey Vessel Kestrel")
	screen.call("SelectContact", 1)
	assert_bool((_find_action_button(screen, "active-scan") as Button).disabled).is_false()


func test_tactical_course_uses_current_core_propulsion_capability_after_allocation() -> void:
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "prioritize:2") as Button).emit_signal("pressed")
	screen.call("ShowTacticalView")
	(_find_action_button(screen, "set-tactical-course") as Button).emit_signal("pressed")
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(0.0, 0.0001)
	assert_str((screen.get_node("%Message") as Label).text).contains(
		"exceeds current propulsion capability"
	)

	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "prioritize:3") as Button).emit_signal("pressed")
	screen.call("ShowTacticalView")
	(_find_action_button(screen, "set-tactical-course") as Button).emit_signal("pressed")
	assert_float(screen.get_meta("tactical_speed", -1.0)).is_equal_approx(2.0, 0.0001)
	assert_str((screen.get_node("%Message") as Label).text).contains("Tactical course set")

	screen.call("ShowEngineeringWorkspace")
	var sensor_priority := _find_engineering_action_button(screen, "prioritize:2") as Button
	assert_bool(sensor_priority.disabled).is_true()
	assert_str(sensor_priority.tooltip_text).contains("current speed")


func test_live_engineering_identity_selection_current_payload_and_traversal_survive_refresh() -> void:
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	var engineering := _engineering_workspace(screen)
	var hierarchy := engineering.get_node("%EngineeringHierarchy")
	var sensor_hierarchy := hierarchy.get_node("Hierarchy_system_2") as Button
	var allocation := _find_engineering_action_button(screen, "prioritize:2") as Button
	var repair := _find_engineering_action_button(screen, "repair:2") as Button
	var hierarchy_id := sensor_hierarchy.get_instance_id()
	var allocation_id := allocation.get_instance_id()
	var repair_id := repair.get_instance_id()
	sensor_hierarchy.emit_signal("pressed")
	allocation.grab_focus()
	assert_bool(allocation.has_focus()).is_true()

	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	await get_tree().process_frame
	var refreshed_allocation := _find_engineering_action_button(screen, "prioritize:2") as Button
	assert_int(
		(hierarchy.get_node("Hierarchy_system_2") as Button).get_instance_id()
	).is_equal(hierarchy_id)
	assert_int(refreshed_allocation.get_instance_id()).is_equal(allocation_id)
	assert_int(
		(_find_engineering_action_button(screen, "repair:2") as Button).get_instance_id()
	).is_equal(repair_id)
	assert_bool(refreshed_allocation.has_focus()).is_true()
	assert_str(engineering.get_meta("selected_component_id", "")).is_equal("system:2")
	assert_bool(refreshed_allocation.focus_next.is_empty()).is_false()
	assert_object(screen.get_node_or_null(refreshed_allocation.focus_next)).is_not_null()

	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	var completed_repair := _find_engineering_action_button(screen, "repair:2") as Button
	assert_int(completed_repair.get_instance_id()).is_equal(repair_id)
	assert_str(completed_repair.tooltip_text).contains("already nominal")
	assert_str(engineering.get_meta("selected_component_id", "")).is_equal("system:2")

	(screen.get_node("%QuickSaveButton") as Button).emit_signal("pressed")
	(screen.get_node("%QuickLoadButton") as Button).emit_signal("pressed")
	assert_str(engineering.get_meta("selected_component_id", "")).is_equal("system:2")
	assert_str(screen.get_meta("active_workspace", "")).is_equal("engineering")


func test_live_impulse_repair_stays_focused_then_submits_through_core() -> void:
	var screen := _create_screen()
	screen.call("QuickSave")
	_write_text(
		TEST_QUICK_SAVE_PATH,
		_rewrite_v5_for_damaged_impulse(
			FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
		)
	)

	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	screen.call("ShowEngineeringWorkspace")
	var impulse_repair := _find_engineering_action_button(screen, "repair:3") as Button
	assert_bool(impulse_repair.disabled).is_false()
	var repair_button_id := impulse_repair.get_instance_id()
	impulse_repair.grab_focus()

	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	await get_tree().process_frame
	var refreshed_repair := _find_engineering_action_button(screen, "repair:3") as Button
	assert_int(refreshed_repair.get_instance_id()).is_equal(repair_button_id)
	assert_bool(refreshed_repair.has_focus()).is_true()
	assert_bool(refreshed_repair.disabled).is_false()

	refreshed_repair.emit_signal("pressed")
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal(
		"BeginRepair:3:Accepted"
	)
	assert_str(screen.get_meta("engineering_repair_target", "")).is_equal("impulse-propulsion")
	assert_str((screen.get_node("%Message") as Label).text).contains(
		"Impulse propulsion repair started"
	)
	assert_int(
		(_find_engineering_action_button(screen, "repair:3") as Button).get_instance_id()
	).is_equal(repair_button_id)
	assert_bool(
		(_find_engineering_action_button(screen, "repair:3") as Button).disabled
	).is_true()


func test_removed_live_engineering_action_cannot_fire_stale_intent_in_preview() -> void:
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	var live_action := _find_engineering_action_button(screen, "prioritize:2") as Button
	live_action.emit_signal("pressed")
	live_action.grab_focus()
	assert_bool(live_action.has_focus()).is_true()
	var command_before: String = screen.get_meta("last_engineering_command", "")
	var allocation_before: int = screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)

	screen.call("ShowPreview", 3)
	assert_object(_find_engineering_action_button(screen, "prioritize:2")).is_null()
	assert_bool(get_viewport().gui_get_focus_owner() == live_action).is_false()
	live_action.emit_signal("pressed")
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal(command_before)
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(allocation_before)
	assert_str(screen.get_meta("data_mode", "")).is_equal("EngineeringPreview")

	screen.call("RestoreLiveMode")
	assert_str(screen.get_meta("data_mode", "")).is_equal("Live")
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(allocation_before)
	assert_bool(
		(_find_engineering_action_button(screen, "prioritize:2") as Button).disabled
	).is_false()


func test_space_pause_does_not_activate_focused_engineering_action() -> void:
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	var action := _find_engineering_action_button(screen, "prioritize:2") as Button
	action.grab_focus()
	var allocation_before: int = screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)
	var command_before: String = screen.get_meta("last_engineering_command", "")

	var press := InputEventKey.new()
	press.physical_keycode = KEY_SPACE
	press.unicode = KEY_SPACE
	press.pressed = true
	screen.call("_Input", press)
	var release := press.duplicate() as InputEventKey
	release.pressed = false
	screen.call("_Input", release)

	assert_float(screen.get_meta("simulation_rate", -1.0)).is_equal(0.0)
	assert_int(screen.get_meta(_installation_meta("allocation", SENSORS_ID), -1)).is_equal(allocation_before)
	assert_str(screen.get_meta("last_engineering_command", "")).is_equal(command_before)
	assert_bool(action.has_focus()).is_true()


func test_live_contact_marker_hit_selects_actor_safe_inspector_context() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	var tactical_map := _command_deck(screen).get_node("%TacticalMap") as Control
	tactical_map.set_anchors_preset(Control.PRESET_TOP_LEFT)
	tactical_map.size = Vector2(800, 500)
	var marker: Vector2 = tactical_map.call("MapContact", 1)

	assert_int(screen.get_meta("sensor_contact_count", 0)).is_equal(1)
	assert_str(screen.get_meta("first_contact_label", "")).is_equal("Contact 1")
	assert_str(screen.get_meta("first_contact_identification", "")).is_equal("Detected")
	assert_str(tactical_map.call("ContactLabel", 1)).is_equal("Contact 1")
	assert_int(tactical_map.call("HitTestContactId", marker)).is_equal(1)
	assert_int(tactical_map.call("HitTestContactId", marker + Vector2(17, 0))).is_equal(0)
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	click.position = marker
	tactical_map.call("_GuiInput", click)

	assert_int(screen.get_meta("selected_contact", 0)).is_equal(1)
	var inspector_text := _collect_control_text(_command_deck(screen).get_node("%InspectorContent"))
	assert_str(inspector_text).contains("LOCAL CONTACT ID")
	assert_str(inspector_text).contains("DETECTED")
	assert_str(inspector_text).contains("OBSERVATION AGE")
	assert_str(inspector_text).not_contains("Survey Vessel Kestrel")
	assert_str(_collect_control_text(screen)).not_contains("Survey Vessel Kestrel")
	assert_bool((_find_action_button(screen, "active-scan") as Button).disabled).is_false()
	assert_bool((_find_action_button(screen, "hail") as Button).disabled).is_true()


func test_tactical_map_keyboard_focus_selects_contact_through_typed_path() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	await get_tree().process_frame
	var tactical_map := _command_deck(screen).get_node("%TacticalMap") as Control
	tactical_map.grab_focus()

	assert_bool(tactical_map.has_focus()).is_true()
	assert_bool(tactical_map.focus_next.is_empty()).is_false()
	var accept := InputEventAction.new()
	accept.action = "ui_accept"
	accept.pressed = true
	tactical_map.call("_GuiInput", accept)

	assert_int(screen.get_meta("selected_contact", 0)).is_equal(1)
	assert_bool(tactical_map.has_focus()).is_true()
	assert_bool((_find_action_button(screen, "active-scan") as Button).disabled).is_false()


func test_tactical_contact_hit_test_uses_distance_then_lowest_local_id() -> void:
	var screen := _create_screen()
	var tactical_map := _command_deck(screen).get_node("%TacticalMap")
	var tactical_origin := Vector2(
		screen.get_meta("tactical_x", 0.0), screen.get_meta("tactical_y", 0.0)
	)
	var click: Vector2 = screen.call(
		"MapTacticalPosition", tactical_origin.x, tactical_origin.y
	)

	assert_int(
		tactical_map.call(
			"HitTestContactCandidates",
			click,
			PackedInt64Array([2, 1]),
			PackedVector2Array([tactical_origin, tactical_origin])
		)
	).is_equal(1)
	assert_int(
		tactical_map.call(
			"HitTestContactCandidates",
			click,
			PackedInt64Array([1, 2]),
			PackedVector2Array(
				[tactical_origin + Vector2(0.2, 0), tactical_origin + Vector2(0.1, 0)]
			)
		)
	).is_equal(2)


func test_contact_selection_survives_refresh_but_load_and_loss_clear_it() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	await get_tree().process_frame
	screen.call("SelectContact", 1)
	var tactical_map := _command_deck(screen).get_node("%TacticalMap")
	var scan_button := _find_action_button(screen, "active-scan") as Button
	var scan_instance_id := scan_button.get_instance_id()
	scan_button.grab_focus()

	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	await get_tree().process_frame
	assert_int(screen.get_meta("selected_contact", 0)).is_equal(1)
	assert_int((_find_action_button(screen, "active-scan") as Button).get_instance_id()).is_equal(
		scan_instance_id
	)
	assert_bool((_find_action_button(screen, "active-scan") as Button).has_focus()).is_true()
	screen.call("QuickSave")
	screen.call("QuickLoad")
	assert_int(screen.get_meta("selected_contact", -1)).is_equal(0)
	assert_int(screen.get_meta("sensor_contact_count", 0)).is_equal(1)

	screen.call("SelectContact", 1)
	for _step in range(34):
		assert_int(screen.call("ProcessSyntheticDelta", 0.6)).is_equal(6)
	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(28600)
	assert_str(screen.get_meta("first_contact_status", "")).is_equal("Stale")
	assert_int(screen.get_meta("selected_contact", 0)).is_equal(1)
	var stale_marker: Vector2 = tactical_map.call("MapContact", 1)
	screen.call("ProcessSyntheticDelta", 0.1)
	var refreshed_stale_marker: Vector2 = tactical_map.call("MapContact", 1)
	assert_bool(refreshed_stale_marker.is_equal_approx(stale_marker)).is_true()
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_int(screen.get_meta("sensor_contact_count", -1)).is_equal(0)
	assert_int(screen.get_meta("selected_contact", -1)).is_equal(0)


func test_active_scan_and_hail_actions_translate_typed_contact_and_reconcile_buttons() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	var scan_button := _find_action_button(screen, "active-scan") as Button
	var hail_button := _find_action_button(screen, "hail") as Button
	var scan_instance_id := scan_button.get_instance_id()
	var hail_instance_id := hail_button.get_instance_id()

	scan_button.grab_focus()
	assert_bool(scan_button.has_focus()).is_true()
	scan_button.emit_signal("pressed")
	await get_tree().process_frame
	assert_str(screen.get_meta("last_contact_command", "")).is_equal("active-scan:Accepted")
	assert_int(screen.get_meta("active_scan_contact", 0)).is_equal(1)
	var disabled_scan := _find_action_button(screen, "active-scan") as Button
	assert_bool(disabled_scan.disabled).is_true()
	assert_int(disabled_scan.get_instance_id()).is_equal(scan_instance_id)
	assert_bool(disabled_scan.has_focus()).is_false()
	var focus_owner := get_viewport().gui_get_focus_owner() as Control
	assert_object(focus_owner).is_not_null()
	assert_bool(focus_owner is Button and (focus_owner as Button).disabled).is_false()
	assert_str(_collect_control_text(_command_deck(screen).get_node("%InspectorContent"))).contains(
		"0 %"
	)
	assert_int(screen.call("ProcessSyntheticDelta", 0.5)).is_equal(5)
	assert_str(_collect_control_text(_command_deck(screen).get_node("%InspectorContent"))).contains(
		"25 %"
	)
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_str(screen.get_meta("first_contact_identification", "")).is_equal("Identified")
	assert_str(screen.get_meta("first_contact_label", "")).is_equal("Survey Vessel Kestrel")
	assert_int((_find_action_button(screen, "active-scan") as Button).get_instance_id()).is_equal(
		scan_instance_id
	)
	assert_int((_find_action_button(screen, "hail") as Button).get_instance_id()).is_equal(
		hail_instance_id
	)
	assert_bool((_find_action_button(screen, "hail") as Button).disabled).is_false()
	assert_str(_collect_control_text(_command_deck(screen))).contains("Pathfinder class")

	screen.call("ShowStrategicView")
	screen.call("RequestSelectedHail")
	assert_str(screen.get_meta("last_contact_command", "")).is_equal("hail:Acknowledged")
	assert_str(screen.get_node("%Message").text).contains("Survey Vessel Kestrel")
	var acknowledged_log := _collect_control_text(screen.get_node("%EventLogContent"))
	assert_str(acknowledged_log).contains("Survey Vessel Kestrel acknowledged the hail")
	assert_str(acknowledged_log).not_contains("Ship 4")
	screen.call("ShowPreview", 2)
	assert_str(_collect_control_text(_command_deck(screen))).not_contains("Survey Vessel Kestrel")
	assert_bool((_find_action_button(screen, "hail-target") as Button).disabled).is_true()


func test_deferred_focus_skips_controls_that_left_the_tree_before_the_frame_flush() -> void:
	# Pins the PR #117 `grab_focus` "!is_inside_tree()" diagnostic. Focus on the tactical-only target
	# selector makes ShowStrategicView queue a fallback onto a live strategic action; the preview in the
	# same frame then reconciles those action buttons out of the tree before the deferred focus runs.
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	var scan_button := _find_action_button(screen, "active-scan") as Button
	scan_button.grab_focus()
	scan_button.emit_signal("pressed")
	await get_tree().process_frame
	var selector := _command_deck(screen).get_node("%TargetSystemSelector") as Control
	assert_object(get_viewport().gui_get_focus_owner()).is_same(selector)

	screen.call("ShowStrategicView")
	var strategic_actions: Array[Node] = _command_deck(screen).get_node("%ContextActions").get_children()
	screen.call("ShowPreview", 2)
	assert_bool(strategic_actions.any(func(action: Node) -> bool: return not action.is_inside_tree())).is_true()

	await assert_error(func() -> void: await get_tree().process_frame).is_success()
	# The workspace entry request queued by ShowPreview is still honoured after the stale one is dropped.
	assert_object(get_viewport().gui_get_focus_owner()).is_same(screen.get_node("%CommandStationButton"))


func test_space_pause_does_not_activate_the_focused_hail_action() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	screen.call("RequestSelectedActiveScan")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	var hail_button := _find_action_button(screen, "hail") as Button
	hail_button.grab_focus()
	assert_bool(hail_button.has_focus()).is_true()
	var log_before := _collect_control_text(screen.get_node("%EventLogContent"))
	var command_before: String = screen.get_meta("last_contact_command", "")

	var press := InputEventKey.new()
	press.physical_keycode = KEY_SPACE
	press.unicode = KEY_SPACE
	press.pressed = true
	screen.call("_Input", press)
	assert_bool(screen.get_viewport().is_input_handled()).is_true()
	var release := press.duplicate() as InputEventKey
	release.pressed = false
	screen.call("_Input", release)
	assert_bool(screen.get_viewport().is_input_handled()).is_true()

	assert_float(screen.get_meta("simulation_rate", -1.0)).is_equal(0.0)
	assert_str(screen.get_meta("last_contact_command", "")).is_equal(command_before)
	assert_str(_collect_control_text(screen.get_node("%EventLogContent"))).is_equal(log_before)
	assert_bool(hail_button.has_focus()).is_true()


func test_travel_departure_presents_contact_and_scan_events_in_log() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	screen.call("RequestSelectedActiveScan")
	screen.call("ShowStrategicView")
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")

	var event_log := _collect_control_text(screen.get_node("%EventLogContent"))
	assert_str(event_log).contains("Contact 1: Contact became stale")
	assert_str(event_log).contains("Contact 1: Active scan interrupted")
	assert_str(event_log).contains("Contact detected")


func test_batched_events_render_their_distinct_core_clocks_in_log() -> void:
	var screen := _create_screen()
	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "prioritize:2") as Button).emit_signal("pressed")
	for _batch in range(12):
		assert_int(screen.call("ProcessSyntheticDelta", 0.6)).is_equal(6)
	assert_int(screen.call("ProcessSyntheticDelta", 0.2)).is_equal(2)
	screen.call("QuickSave")

	var save_text := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var knowledge_start := save_text.find('"sensorKnowledge":')
	var contacts_member := save_text.find('"contacts":', knowledge_start)
	var contacts_start := save_text.find("[", contacts_member)
	var active_scan_start := save_text.find('"activeScan":', contacts_start)
	var contacts_end := save_text.rfind("]", active_scan_start)
	assert_int(knowledge_start).is_greater_equal(0)
	assert_int(contacts_member).is_greater_equal(knowledge_start)
	assert_int(contacts_start).is_greater(contacts_member)
	assert_int(active_scan_start).is_greater(contacts_start)
	assert_int(contacts_end).is_greater(contacts_start)
	save_text = save_text.left(contacts_start + 1) + save_text.substr(contacts_end)
	_write_text(TEST_QUICK_SAVE_PATH, save_text)
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")

	assert_int(screen.call("ProcessSyntheticDelta", 0.6)).is_equal(6)
	var event_log := _collect_control_text(screen.get_node("%EventLogContent"))
	assert_str(event_log).contains("00:00:07  SENSOR")
	assert_str(event_log).contains("Contact detected")
	assert_str(event_log).contains("00:00:08  ENGINEER")
	assert_str(event_log).contains("Sensors repair completed")


func test_hail_no_response_appears_in_log_without_hidden_ship_identity() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	screen.call("RequestSelectedActiveScan")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("RequestSelectedHail")

	assert_str(screen.get_meta("last_contact_command", "")).is_equal("hail:NoResponse")
	var event_log := _collect_control_text(screen.get_node("%EventLogContent"))
	assert_str(event_log).contains("USS Wayfarer did not respond")
	assert_str(event_log).not_contains("Ship 2")


func test_live_unsupported_values_and_engineering_actions_are_explicitly_unavailable() -> void:
	var screen := _create_screen()
	assert_str(_collect_control_text(_command_deck(screen))).contains("UNAVAILABLE")
	assert_bool((_find_action_button(screen, "fire-phasers") as Button).disabled).is_true()

	screen.call("ShowEngineeringWorkspace")
	var hierarchy_text := _collect_control_text(
		_engineering_workspace(screen).get_node("%EngineeringHierarchy")
	)
	assert_str(hierarchy_text).contains("SHIELDS")
	assert_str(hierarchy_text).contains("DIRECTED-ENERGY WEAPONS")
	assert_str(hierarchy_text).not_contains("EPS")
	assert_bool((_find_engineering_action_button(screen, "balance") as Button).disabled).is_false()
	assert_bool(
		_engineering_workspace(screen).get_node("%EngineeringSchematic").get_meta(
			"topology_available", true
		)
	).is_false()


func test_invalid_content_bootstrap_is_fail_closed_and_player_safe() -> void:
	_write_text(INVALID_CONTENT_PATH, "not-json")
	var scene := _load_main_scene()
	var screen := _track_screen(scene.instantiate())
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
	assert_str(presented).not_contains("Expedition Vessel Aurora")
	assert_str(presented).not_contains("Expedition Vessel Resolute")
	assert_str(presented).not_contains("Faction A")
	assert_str(presented).not_contains("Faction B")
	assert_str(presented).not_contains("OrderWake")
	assert_str(presented).not_contains("ScheduledWork")
	assert_str(presented).not_contains("FactionDecisionWake")
	assert_str(presented).not_contains("ObservationReportDelivery")
	assert_str(presented).not_contains("reportDelivery")
	assert_str(presented).not_contains("activeInvestigation")
	assert_str(presented).not_contains("receivedReports")
	assert_str(presented).not_contains("completionWatermarks")


func test_production_observation_response_remains_actor_safe_until_sensor_arrival() -> void:
	var screen := _create_screen()
	_advance_fixed_steps(screen, 160)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(16000)
	screen.call("QuickSave")
	var response_save: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	)
	var simulation: Dictionary = response_save.get("simulation", {})
	var factions: Array = simulation.get("factions", [])
	var ships: Array = simulation.get("ships", [])
	var observer_ids: Array[int] = []
	var responder_ids: Array[int] = []
	var hidden_ship_names: Array[String] = []
	assert_int(factions.size()).is_equal(2)
	for faction in factions:
		var faction_id := int(faction.get("id", 0))
		var observation: Dictionary = faction.get("observation", {})
		var investigation: Dictionary = observation.get("activeInvestigation", {})
		var source_report: Dictionary = investigation.get("sourceReport", {})
		var observer_id := int(source_report.get("observerShipId", 0))
		var responder_id := int(investigation.get("responderShipId", 0))
		assert_str(observation.get("posture", "")).is_equal("enabled")
		assert_int((observation.get("receivedReports", []) as Array).size()).is_greater(0)
		assert_bool(investigation.is_empty()).is_false()
		assert_int(observer_id).is_not_equal(1)
		assert_int(responder_id).is_not_equal(1)
		assert_int(observer_id).is_not_equal(responder_id)
		assert_int(
			int(_find_saved_ship(ships, observer_id).get("directControllerFactionId", 0))
		).is_equal(faction_id)
		assert_int(
			int(_find_saved_ship(ships, responder_id).get("directControllerFactionId", 0))
		).is_equal(faction_id)
		assert_str(investigation.get("destinationLocationId", "")).is_equal("vesper-reach")
		observer_ids.append(observer_id)
		responder_ids.append(responder_id)
		hidden_ship_names.append(_find_saved_ship(ships, observer_id).get("displayName", ""))
		hidden_ship_names.append(_find_saved_ship(ships, responder_id).get("displayName", ""))
	assert_int(observer_ids[0]).is_not_equal(observer_ids[1])
	assert_int(responder_ids[0]).is_not_equal(responder_ids[1])
	var presented_at_dawn := _collect_control_text(screen)
	for hidden_ship_name in hidden_ship_names:
		assert_str(hidden_ship_name).is_not_empty()
		assert_str(presented_at_dawn).not_contains(hidden_ship_name)
	for hidden_term in [
		"Faction A",
		"Faction B",
		"activeInvestigation",
		"receivedReports",
		"reportDelivery",
	]:
		assert_str(presented_at_dawn).not_contains(hidden_term)

	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")
	var player_arrival: int = screen.get_meta("travel_eta_milliseconds", -1)
	assert_int(player_arrival).is_equal(28000)
	_advance_fixed_steps(screen, int((player_arrival - 16000) / 100))
	assert_bool(screen.get_meta("travel_active", true)).is_false()
	screen.call("QuickSave")
	var before_arrival_save: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	)
	var before_arrival_simulation: Dictionary = before_arrival_save.get("simulation", {})
	var player_before := _find_saved_ship(before_arrival_simulation.get("ships", []), 1)
	var contacts_before: Array = player_before.get("sensorKnowledge", {}).get("contacts", [])
	var visible_contact_count_before: int = screen.get_meta("sensor_contact_count", -1)
	var responder_arrival := 0
	for responder_id in responder_ids:
		assert_bool(_find_saved_contact(contacts_before, responder_id).is_empty()).is_true()
		var responder := _find_saved_ship(before_arrival_simulation.get("ships", []), responder_id)
		var expected_arrival := int(
			responder.get("strategicState", {}).get("travel", {}).get(
				"expectedArrivalMilliseconds", 0
			)
		)
		if responder_arrival == 0 or expected_arrival < responder_arrival:
			responder_arrival = expected_arrival
	assert_int(responder_arrival).is_equal(30000)
	_advance_fixed_steps(screen, int((responder_arrival - player_arrival) / 100))
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(responder_arrival)
	screen.call("QuickSave")
	var arrival_save: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	)
	var arrival_simulation: Dictionary = arrival_save.get("simulation", {})
	var player_after := _find_saved_ship(arrival_simulation.get("ships", []), 1)
	var contacts_after: Array = player_after.get("sensorKnowledge", {}).get("contacts", [])
	var responder_contact_count := 0
	var presented_on_arrival := _collect_control_text(screen)
	for responder_id in responder_ids:
		var responder_contact := _find_saved_contact(contacts_after, responder_id)
		if responder_contact.is_empty():
			continue
		responder_contact_count += 1
		assert_str(responder_contact.get("status", "")).is_equal("current")
		assert_str(presented_on_arrival).contains(
			"Contact %d" % int(responder_contact.get("id", 0))
		)
	assert_int(responder_contact_count).is_greater(0)
	assert_int(screen.get_meta("sensor_contact_count", -1)).is_greater(visible_contact_count_before)
	for hidden_ship_name in hidden_ship_names:
		assert_str(presented_on_arrival).not_contains(hidden_ship_name)
	assert_str(presented_on_arrival).not_contains("Pathfinder class")
	assert_str(presented_on_arrival).not_contains("Faction A")
	assert_str(presented_on_arrival).not_contains("Faction B")


func test_quick_load_rejects_actual_player_sourced_report_without_damaging_live_state() -> void:
	var screen := _create_screen()
	_advance_fixed_steps(screen, 140)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(14000)
	screen.call("QuickSave")
	var save_text := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var save_json: Dictionary = JSON.parse_string(save_text)
	var factions: Array = save_json.get("simulation", {}).get("factions", [])
	var actual_report: Dictionary = {}
	for faction in factions:
		var in_flight: Array = faction.get("observation", {}).get("inFlightReports", [])
		if not in_flight.is_empty():
			actual_report = in_flight[0].get("report", {})
			break
	assert_bool(actual_report.is_empty()).is_false()
	var report_id := int(actual_report.get("reportId", 0))
	var observer_id := int(actual_report.get("observerShipId", 0))
	assert_int(report_id).is_greater(0)
	assert_int(observer_id).is_not_equal(1)
	var observer_pattern := RegEx.new()
	assert_int(
		observer_pattern.compile(
			'"reportId"\\s*:\\s*%d\\s*,\\s*"observerShipId"\\s*:\\s*%d\\s*,'
			% [report_id, observer_id]
		)
	).is_equal(OK)
	assert_int(observer_pattern.search_all(save_text).size()).is_equal(1)
	_write_text(
		TEST_QUICK_SAVE_PATH,
		observer_pattern.sub(save_text, '"reportId":%d,"observerShipId":1,' % report_id)
	)
	var retained_time: int = screen.get_meta("simulation_time_milliseconds", -1)
	var retained_ship_name: String = screen.get_meta("ship_name", "")

	screen.call("QuickLoad")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("load_failed")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(retained_time)
	assert_str(screen.get_meta("ship_name", "")).is_equal(retained_ship_name)
	assert_str(screen.get_meta("data_mode", "")).is_equal("Live")
	assert_bool(screen.get_meta("travel_active", true)).is_false()
	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")
	assert_bool(screen.get_meta("travel_active", false)).is_true()
	assert_int(screen.get_meta("travel_eta_milliseconds", -1)).is_equal(retained_time + 12000)


func test_default_quick_save_writes_production_v9_without_touching_legacy_slot() -> void:
	_write_text(LEGACY_DEFAULT_QUICK_SAVE_PATH, "legacy-slot-sentinel")
	var screen := _create_default_screen()

	screen.call("QuickSave")

	assert_str(screen.get_meta("quick_save_status", "")).is_equal("saved")
	assert_bool(FileAccess.file_exists(DEFAULT_QUICK_SAVE_PATH)).is_true()
	var save_json: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(DEFAULT_QUICK_SAVE_PATH)
	)
	assert_int(int(save_json.get("schemaVersion", -1))).is_equal(10)
	assert_str(save_json.get("simulationRulesVersion", "")).is_equal(
		"installed-ship-system-substrate-v1"
	)
	var simulation: Dictionary = save_json.get("simulation", {})
	assert_int((simulation.get("factions", []) as Array).size()).is_equal(2)
	var ships: Array = simulation.get("ships", [])
	assert_bool(_find_saved_ship(ships, 1).get("directControllerFactionId", 0) == null).is_true()
	assert_int(int(_find_saved_ship(ships, 2).get("directControllerFactionId", 0))).is_equal(2)
	assert_int(int(_find_saved_ship(ships, 5).get("directControllerFactionId", 0))).is_equal(1)
	assert_int(int(_find_saved_ship(ships, 6).get("directControllerFactionId", 0))).is_equal(1)
	assert_str(FileAccess.get_file_as_string(LEGACY_DEFAULT_QUICK_SAVE_PATH)).is_equal(
		"legacy-slot-sentinel"
	)


func test_default_quick_load_discovers_legacy_slot_path_then_saves_generic_v9() -> void:
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
	assert_int(int(save_json.get("schemaVersion", -1))).is_equal(10)
	assert_str(save_json.get("simulationRulesVersion", "")).is_equal(
		"installed-ship-system-substrate-v1"
	)
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
	var scene := _load_main_scene()
	var screen := _track_screen(scene.instantiate())
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


func test_quick_save_load_retains_faction_travel_and_continues_to_satisfied_presence() -> void:
	var screen := _create_screen()
	assert_int(screen.call("ProcessSyntheticDelta", 0.1)).is_equal(1)
	screen.call("QuickSave")

	var assigned_save: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	)
	var assigned_simulation: Dictionary = assigned_save.get("simulation", {})
	var assigned_ship := _find_saved_ship(assigned_simulation.get("ships", []), 5)
	var assigned_faction := _find_saved_faction(assigned_simulation.get("factions", []), 1)
	assert_str(assigned_ship.get("strategicState", {}).get("kind", "")).is_equal("traveling")
	assert_str(assigned_ship.get("activeOrder", {}).get("kind", "")).is_equal("travelTo")
	assert_str(assigned_ship.get("activeOrder", {}).get("destination", "")).is_equal(
		"vesper-reach"
	)
	assert_int(int(assigned_ship.get("directControllerFactionId", 0))).is_equal(1)
	assert_str(assigned_faction.get("presenceObjective", {}).get("status", "")).is_equal(
		"assigned"
	)
	assert_int(
		int(assigned_faction.get("presenceObjective", {}).get("assignedShipId", 0))
	).is_equal(5)

	for _step in range(5):
		screen.call("ProcessSyntheticDelta", 0.6)
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(3100)
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(100)

	for _step in range(24):
		screen.call("ProcessSyntheticDelta", 0.6)
	screen.call("QuickSave")
	var continued_save: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	)
	var continued_simulation: Dictionary = continued_save.get("simulation", {})
	var arrived_ship := _find_saved_ship(continued_simulation.get("ships", []), 5)
	var satisfied_faction := _find_saved_faction(continued_simulation.get("factions", []), 1)
	assert_str(arrived_ship.get("strategicState", {}).get("kind", "")).is_equal("atLocation")
	assert_str(arrived_ship.get("strategicState", {}).get("locationId", "")).is_equal(
		"vesper-reach"
	)
	assert_bool(arrived_ship.get("activeOrder", null) == null).is_true()
	assert_str(satisfied_faction.get("presenceObjective", {}).get("status", "")).is_equal(
		"satisfied"
	)


func test_malformed_current_controller_and_scheduler_targets_leave_live_simulation_usable() -> void:
	var screen := _create_screen()
	screen.call("QuickSave")
	var valid_save := FileAccess.get_file_as_string(TEST_QUICK_SAVE_PATH)
	var parsed_save: Dictionary = JSON.parse_string(valid_save)
	var faction_work_id := 0
	for work in parsed_save.get("simulation", {}).get("scheduler", {}).get("outstandingWork", []):
		if int(work.get("targetFactionId", 0)) == 1:
			faction_work_id = int(work.get("id", 0))
			break
	assert_int(faction_work_id).is_greater(0)
	var retained_identity: int = screen.get_meta("simulation_identity", 0)

	var invalid_saves := [
		_replace_saved_object_field(
			valid_save, "ships", "instanceId", 2, "directControllerFactionId", 2, 99
		),
		_replace_saved_object_field(
			valid_save,
			"outstandingWork",
			"id",
			faction_work_id,
			"targetFactionId",
			1,
			99
		),
	]
	for invalid_save in invalid_saves:
		_write_text(TEST_QUICK_SAVE_PATH, invalid_save)
		screen.call("QuickLoad")
		assert_str(screen.get_meta("quick_save_status", "")).is_equal("load_failed")
		assert_int(screen.get_meta("simulation_identity", 0)).is_equal(retained_identity)
		assert_int(screen.get_meta("simulation_time_milliseconds", -1)).is_equal(0)

	screen.call("SelectDestination", "vesper-reach")
	assert_bool((screen.get_node("%TravelButton") as Button).disabled).is_false()
	screen.call("RequestSelectedTravel")
	assert_bool(screen.get_meta("travel_active", false)).is_true()


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


func test_unconnected_destination_submits_intent_and_core_rejects_route() -> void:
	var screen := _create_screen()
	screen.call("SelectDestination", "meridian-drift")
	var travel_button := screen.get_node("%TravelButton") as Button
	assert_bool(travel_button.disabled).is_false()

	travel_button.emit_signal("pressed")

	assert_bool(screen.get_meta("travel_active", false)).is_false()
	assert_str(screen.get_node("%Message").text).contains("no direct route is known")
	assert_str(screen.get_meta("selected_destination", "")).is_equal("meridian-drift")


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
		"Sensors repair completed"
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


func _prepare_detected_contact(screen: Node) -> void:
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	screen.call("ShowEngineeringWorkspace")
	(_find_engineering_action_button(screen, "prioritize:2") as Button).emit_signal("pressed")
	assert_int(screen.get_meta("sensor_contact_count", 0)).is_equal(1)


func _advance_fixed_steps(screen: Node, fixed_steps: int) -> void:
	assert_int(fixed_steps).is_greater_equal(0)
	var remaining := fixed_steps
	while remaining > 0:
		var batch := mini(remaining, 6)
		assert_int(screen.call("ProcessSyntheticDelta", batch * 0.1)).is_equal(batch)
		remaining -= batch


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
	var scene := _load_main_scene()
	var screen := _track_screen(scene.instantiate())
	screen.set("QuickSaveUserPath", TEST_QUICK_SAVE_PATH)
	add_child(screen)
	screen.set_process(false)
	return screen


func _create_default_screen() -> Node:
	var scene := _load_main_scene()
	var screen := _track_screen(scene.instantiate())
	add_child(screen)
	screen.set_process(false)
	return screen


func _track_screen(screen: Node) -> Node:
	# GdUnit's immediate free path can invalidate child C# wrappers before Godot's managed bridge observes teardown.
	# Queueing the owned scene and awaiting one frame in after_test preserves the normal engine lifecycle.
	_screens_to_free.append(screen)
	return screen


func _load_main_scene() -> PackedScene:
	# Repeated cached mixed-language scene instantiation can race Godot's managed-handle replacement during teardown.
	# Tests need isolated scene resources; production still loads its one ordinary cached startup scene.
	return ResourceLoader.load(
		"res://Main.tscn", "", ResourceLoader.CACHE_MODE_IGNORE_DEEP
	) as PackedScene


func _command_deck(screen: Node) -> Node:
	return screen.get_node("%CommandDeckWorkspace")


func _engineering_workspace(screen: Node) -> Node:
	return screen.get_node("%EngineeringWorkspace")


func _find_action_button(screen: Node, action_id: String) -> Button:
	for child in _command_deck(screen).get_node("%ContextActions").get_children():
		if child is Button and child.name == "Action_" + action_id:
			return child
	return null


func _installation_meta(field: String, installed_id: int) -> String:
	return "engineering_%s_%d" % [field, installed_id]


func _regex_replace_first(source: String, pattern: String, replacement: String) -> String:
	var regex := RegEx.new()
	assert_int(regex.compile(pattern)).is_equal(OK)
	assert_object(regex.search(source)).is_not_null()
	return regex.sub(source, replacement, false)


func _shift_ship_ids(source: String, offset: int) -> String:
	# Renumbers every ship identity and ship reference in raw save text, keeping ship order and integer tokens.
	# Every ship-reference member of the save DTOs is listed; null references (faction targets) do not match.
	var regex := RegEx.new()
	assert_int(
		regex.compile('"(instanceId|playerShipId|targetShipId|observerShipId|responderShipId|assignedShipId|shipAllocatorNextId)"(\\s*):(\\s*)(\\d+)')
	).is_equal(OK)
	var shifted := ""
	var cursor := 0
	var matches := regex.search_all(source)
	assert_int(matches.size()).is_greater(0)
	for found in matches:
		shifted += source.substr(cursor, found.get_start() - cursor)
		shifted += '"%s"%s:%s%d' % [
			found.get_string(1), found.get_string(2), found.get_string(3), int(found.get_string(4)) + offset
		]
		cursor = found.get_end()
	return shifted + source.substr(cursor)


func _button_texts(container: Node) -> Array[String]:
	var texts: Array[String] = []
	for child in container.get_children():
		if child is Button:
			texts.append("%s=%s" % [child.name, (child as Button).text])
	return texts


func _section_rows(container: Node) -> Array[String]:
	# Headings render as bare labels and fields as label/value rows; see EngineeringWorkspace.RebuildSection.
	var rows: Array[String] = []
	for child in container.get_children():
		if child is Label:
			rows.append((child as Label).text)
		elif child is HBoxContainer and child.get_child_count() == 2:
			rows.append("%s=%s" % [(child.get_child(0) as Label).text, (child.get_child(1) as Label).text])
	return rows


func _find_engineering_action_button(screen: Node, action_id: String) -> Button:
	for child in _engineering_workspace(screen).get_node("%EngineeringActionsContent").get_children():
		if child is Button and child.name == "Action_" + action_id.replace("-", "_").replace(":", "_"):
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


func _find_saved_ship(ships: Array, instance_id: int) -> Dictionary:
	for ship in ships:
		if int(ship.get("instanceId", 0)) == instance_id:
			return ship
	return {}


func _find_saved_faction(factions: Array, faction_id: int) -> Dictionary:
	for faction in factions:
		if int(faction.get("id", 0)) == faction_id:
			return faction
	return {}


func _find_saved_contact(contacts: Array, target_ship_id: int) -> Dictionary:
	for contact in contacts:
		if int(contact.get("targetShipId", 0)) == target_ship_id:
			return contact
	return {}


func _replace_saved_object_field(
	source: String,
	collection_name: String,
	identity_name: String,
	identity_value: int,
	field_name: String,
	old_value,
	new_value
) -> String:
	# Preserve every other raw JSON token while bounding the edit to one parsed object's stable identity.
	# Re-serializing through Variant can normalize strict integer tokens and make the fixture fail for the wrong reason.
	var field_match := _find_saved_object_field(
		source, collection_name, identity_name, identity_value, field_name
	)
	var value_pattern := RegEx.new()
	assert_int(
		value_pattern.compile('\\s*%s(?=\\s*[,}])' % str(old_value))
	).is_equal(OK)
	var value_match := value_pattern.search(source, field_match.get_end())
	assert_object(value_match).is_not_null()
	return source.left(value_match.get_start()) + str(new_value) + source.substr(value_match.get_end())


func _find_saved_object_field(
	source: String,
	collection_name: String,
	identity_name: String,
	identity_value: int,
	field_name: String
) -> RegExMatch:
	var collection_index := source.find('"%s"' % collection_name)
	assert_int(collection_index).is_greater_equal(0)
	var identity_pattern := RegEx.new()
	assert_int(
		identity_pattern.compile('"%s"\\s*:\\s*%d\\s*[,}]' % [identity_name, identity_value])
	).is_equal(OK)
	var identity_match := identity_pattern.search(source, collection_index)
	assert_object(identity_match).is_not_null()
	var field_pattern := RegEx.new()
	assert_int(field_pattern.compile('"%s"\\s*:' % field_name)).is_equal(OK)
	var field_match := field_pattern.search(source, identity_match.get_end())
	assert_object(field_match).is_not_null()
	var next_identity_pattern := RegEx.new()
	assert_int(next_identity_pattern.compile('"%s"\\s*:' % identity_name)).is_equal(OK)
	var next_identity := next_identity_pattern.search(source, identity_match.get_end())
	assert_bool(next_identity == null or field_match.get_start() < next_identity.get_start()).is_true()
	return field_match


func _replace_once(source: String, old_value: String, new_value: String) -> String:
	var match_index := source.find(old_value)
	assert_int(match_index).is_greater_equal(0)
	assert_int(source.find(old_value, match_index + old_value.length())).is_equal(-1)
	return source.left(match_index) + new_value + source.substr(match_index + old_value.length())


func _rewrite_v5_for_damaged_impulse(save_text: String) -> String:
	# Godot's JSON parser normalizes integer tokens through Variant; targeted structural edits keep the strict
	# persistence contract intact while constructing one otherwise unreachable damaged-impulse test fixture.
	var parsed: Dictionary = JSON.parse_string(save_text)
	var simulation: Dictionary = parsed.get("simulation", {})
	var player_ship_id := int(simulation.get("playerShipId", 0))
	var player_ship := _find_saved_ship(simulation.get("ships", []), player_ship_id)
	var repair: Dictionary = player_ship.get("engineering", {}).get("activeRepair", {})
	var repair_work_id := int(repair.get("scheduledCompletionId", 0))
	assert_int(player_ship_id).is_greater(0)
	assert_bool(repair.is_empty()).is_false()
	assert_int(repair_work_id).is_greater(0)
	# V10 installations are ship-local; the player (ship 1) is written first, so the first installedSystems array
	# is the player's. Production installed id 3 is impulse.
	assert_int(player_ship_id).is_equal(1)
	save_text = _replace_saved_object_field(
		save_text, "installedSystems", "installedSystemId", 3, "condition", 1, 0.5
	)

	var repair_member := _find_saved_object_field(
		save_text, "ships", "instanceId", player_ship_id, "activeRepair"
	)
	var repair_value_start := save_text.find("{", repair_member.get_end())
	var repair_value_end := save_text.find("}", repair_value_start)
	assert_int(repair_value_start).is_greater_equal(repair_member.get_end())
	assert_int(repair_value_end).is_greater(repair_value_start)
	save_text = save_text.left(repair_value_start) + "null" + save_text.substr(repair_value_end + 1)

	var work_id_match := _find_saved_object_field(
		save_text, "outstandingWork", "id", repair_work_id, "kind"
	)
	var work_start := save_text.rfind("{", work_id_match.get_start())
	var work_end := save_text.find("}", work_id_match.get_end())
	assert_int(work_start).is_greater_equal(0)
	assert_int(work_end).is_greater(work_id_match.get_end())
	var work: Dictionary = JSON.parse_string(save_text.substr(work_start, work_end - work_start + 1))
	assert_str(work.get("kind", "")).is_equal("systemRepairCompletion")
	assert_int(int(work.get("targetShipId", 0))).is_equal(player_ship_id)
	var removal_start := work_start
	var removal_end := work_end + 1
	while removal_end < save_text.length() and save_text[removal_end] in [" ", "\n", "\r", "\t"]:
		removal_end += 1
	if removal_end < save_text.length() and save_text[removal_end] == ",":
		removal_end += 1
	else:
		var preceding := work_start - 1
		while preceding >= 0 and save_text[preceding] in [" ", "\n", "\r", "\t"]:
			preceding -= 1
		assert_bool(preceding >= 0 and save_text[preceding] == ",").is_true()
		removal_start = preceding
	return save_text.left(removal_start) + save_text.substr(removal_end)


func _remove_quick_save_files() -> void:
	_remove_file(TEST_QUICK_SAVE_PATH)
	_remove_file(DEFAULT_QUICK_SAVE_PATH)
	_remove_file(LEGACY_DEFAULT_QUICK_SAVE_PATH)


func _remove_file(user_path: String) -> void:
	if FileAccess.file_exists(user_path):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(user_path))


func test_strategic_last_known_report_outlives_tactical_loss_travel_and_reload() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowTacticalView")
	screen.call("SelectContact", 1)
	screen.call("RequestSelectedActiveScan")
	screen.call("AdvanceUntilNextPlayerRelevantEvent")
	assert_str(screen.get_meta("first_contact_identification", "")).is_equal("Identified")
	# Read the observation time from the tactical inspector rather than the simulation clock: the
	# report must repeat the observation Core recorded, which is not necessarily "now".
	var observed_at := _tactical_observed_at(screen)
	assert_str(observed_at).is_not_empty()
	screen.call("ShowStrategicView")

	var identified_row := _contact_report_value(screen, "Survey Vessel Kestrel")
	assert_str(identified_row).contains("Last seen at Dawn Anchor")
	assert_str(identified_row).contains("t=%s" % observed_at)
	assert_str(identified_row).contains("CURRENT")

	# Driven by elapsed simulation time rather than the 24100/29100 ms literals: staleness and loss
	# are time-driven decays, and AdvanceUntilNextPlayerRelevantEvent stops advancing once the scan
	# has resolved and nothing else is pending.
	for _step in range(80):
		if screen.get_meta("first_contact_status", "") == "Stale":
			break
		screen.call("ProcessSyntheticDelta", 0.6)
	assert_str(screen.get_meta("first_contact_status", "")).is_equal("Stale")
	assert_str(_contact_report_value(screen, "Survey Vessel Kestrel")).contains("STALE")
	assert_str(_contact_report_tone(screen, "Survey Vessel Kestrel")).is_equal("StatusCaution")

	for _step in range(80):
		if screen.get_meta("sensor_contact_count", -1) == 0:
			break
		screen.call("ProcessSyntheticDelta", 0.6)
	assert_int(screen.get_meta("sensor_contact_count", -1)).is_equal(0)
	screen.call("ShowTacticalView")
	assert_str(_collect_control_text(_command_deck(screen).get_node("%TacticalMap"))).not_contains(
		"Survey Vessel Kestrel"
	)
	screen.call("ShowStrategicView")
	var lost_row := _contact_report_value(screen, "Survey Vessel Kestrel")
	assert_str(lost_row).contains("Last seen at Dawn Anchor")
	assert_str(lost_row).contains("LOST")
	assert_str(_contact_report_tone(screen, "Survey Vessel Kestrel")).is_equal("MutedTelemetry")
	assert_str(_report_observed_at(lost_row)).is_not_empty()

	screen.call("SelectDestination", "vesper-reach")
	screen.call("RequestSelectedTravel")
	for _step in range(20):
		screen.call("ProcessSyntheticDelta", 0.6)
	assert_bool(screen.get_meta("travel_active", true)).is_false()
	assert_str(_collect_control_text(_command_deck(screen))).contains("Vesper Reach")
	# Arriving elsewhere must not re-frame the observation. Only the Kestrel row is compared because
	# fresh contacts observed at the new location legitimately add rows of their own.
	assert_str(_contact_report_value(screen, "Survey Vessel Kestrel")).is_equal(lost_row)

	screen.call("QuickSave")
	screen.call("QuickLoad")
	assert_str(screen.get_meta("quick_save_status", "")).is_equal("loaded")
	assert_str(_contact_report_value(screen, "Survey Vessel Kestrel")).is_equal(lost_row)


func test_strategic_last_known_section_shows_only_projection_sourced_actor_safe_facts() -> void:
	var screen := _create_screen()

	assert_str(_last_known_contacts_text(screen)).contains("No retained contact reports")

	_prepare_detected_contact(screen)
	screen.call("ShowStrategicView")
	var unidentified_report := _last_known_contacts_text(screen)
	assert_str(unidentified_report).contains("Contact 1")
	assert_str(unidentified_report).not_contains("Survey Vessel Kestrel")
	assert_str(unidentified_report).not_contains("IKS Rotarran")

	var presented := _collect_control_text(screen)
	assert_str(presented).not_contains("USS Wayfarer")
	assert_str(presented).not_contains("USS Horizon")
	assert_str(presented).not_contains("OrderWake")
	assert_str(presented).not_contains("ScheduledWork")
	assert_str(presented).not_contains("IKS Rotarran")

	screen.call("ShowPreview", 1)
	assert_str(_last_known_contacts_text(screen)).contains("IKS Rotarran")
	screen.call("ShowStrategicView")
	assert_str(_last_known_contacts_text(screen)).not_contains("IKS Rotarran")
	assert_str(_last_known_contacts_text(screen)).contains("Contact 1")


func test_strategic_last_known_refresh_preserves_reconciled_action_focus() -> void:
	var screen := _create_screen()
	_prepare_detected_contact(screen)
	screen.call("ShowStrategicView")
	screen.call("SelectDestination", "vesper-reach")
	await get_tree().process_frame
	var travel_action := _find_action_button(screen, "travel") as Button
	var travel_instance_id := travel_action.get_instance_id()
	travel_action.grab_focus()
	assert_bool(travel_action.has_focus()).is_true()
	var report_before := _last_known_contacts_text(screen)

	# Advance until the retained report actually changes (a fresh observation time, then Stale, then
	# Lost all rewrite it) so this pins focus stability across a refresh that rebuilds the section.
	for _step in range(60):
		screen.call("ProcessSyntheticDelta", 0.6)
		if _last_known_contacts_text(screen) != report_before:
			break
	await get_tree().process_frame

	# The section is rebuilt content, not an interactive control, so a changing report must not
	# move focus off the reconciled action button.
	assert_str(_last_known_contacts_text(screen)).is_not_equal(report_before)
	assert_int((_find_action_button(screen, "travel") as Button).get_instance_id()).is_equal(
		travel_instance_id
	)
	assert_bool((_find_action_button(screen, "travel") as Button).has_focus()).is_true()


func _last_known_contacts_panel(screen: Node) -> Node:
	return (
		_command_deck(screen)
		. get_node("%InspectorContent")
		. get_node_or_null("Telemetry_last-known-contacts")
	)


func _last_known_contacts_text(screen: Node) -> String:
	var panel := _last_known_contacts_panel(screen)
	if panel == null:
		return ""
	return _collect_control_text(panel)


func _contact_report_row(screen: Node, label: String) -> Node:
	var panel := _last_known_contacts_panel(screen)
	if panel == null:
		return null
	# Child 0 of the section body is the heading label; every later child is one field row of
	# [label, value], matching CommandDeckWorkspace.PresentInspector.
	var body := panel.get_child(0)
	for index in range(1, body.get_child_count()):
		var row := body.get_child(index)
		if (row.get_child(0) as Label).text == label:
			return row
	return null


func _contact_report_value(screen: Node, label: String) -> String:
	var row := _contact_report_row(screen, label)
	return "" if row == null else (row.get_child(1) as Label).text


func _contact_report_tone(screen: Node, label: String) -> String:
	var row := _contact_report_row(screen, label)
	return "" if row == null else str((row.get_child(1) as Label).theme_type_variation)


func _tactical_observed_at(screen: Node) -> String:
	# The tactical inspector's OBSERVED AT field is the same Core observation timestamp the strategic
	# report repeats, so comparing against it pins agreement between the two surfaces.
	return _match_group(
		_collect_control_text(_command_deck(screen).get_node("%InspectorContent")),
		"OBSERVED AT\\s+([0-9.]+ s)"
	)


func _report_observed_at(report_text: String) -> String:
	return _match_group(report_text, "t=([0-9.]+ s)")


func _match_group(text: String, pattern: String) -> String:
	var regex := RegEx.new()
	regex.compile(pattern)
	var found := regex.search(text)
	return "" if found == null else found.get_string(1)
