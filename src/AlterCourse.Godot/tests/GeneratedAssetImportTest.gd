class_name GeneratedAssetImportTest
extends GdUnitTestSuite


func test_assetctl_generated_png_imports_as_texture() -> void:
	var texture := load("res://assets/assetctl-fixtures/generated-marker.png") as Texture2D
	assert_object(texture).is_not_null()
	assert_int(texture.get_width()).is_equal(80)
	assert_int(texture.get_height()).is_equal(80)


func test_assetctl_sanitized_svg_imports_as_texture() -> void:
	var texture := load("res://assets/assetctl-fixtures/generated-marker.svg") as Texture2D
	assert_object(texture).is_not_null()
	assert_int(texture.get_width()).is_equal(64)
	assert_int(texture.get_height()).is_equal(64)
