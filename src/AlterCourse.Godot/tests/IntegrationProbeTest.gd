class_name IntegrationProbeTest
extends GdUnitTestSuite


func test_csharp_node_enters_the_scene_tree() -> void:
	var scene := load("res://tests/IntegrationProbe.tscn") as PackedScene
	var probe: Node = auto_free(scene.instantiate())
	add_child(probe)

	assert_bool(probe.get_meta("csharp_entered_tree", false)).is_true()
