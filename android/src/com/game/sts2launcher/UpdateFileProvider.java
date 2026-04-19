package com.game.sts2launcher;

import androidx.core.content.FileProvider;

/**
 * Dedicated FileProvider subclass just so the Gradle manifest merger doesn't
 * conflict with the one Godot's template AAR already registers under the
 * {@code androidx.core.content.FileProvider} class. Both providers coexist
 * because they have distinct class names and authorities.
 */
public class UpdateFileProvider extends FileProvider {
}
