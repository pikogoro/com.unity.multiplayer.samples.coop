# README

## FPS

1. Open "Edit" -> "Project Settings..." -> "Player".
2. Expand "Other Settings".
3. Add "P56" symbol to "Scripting Defines Symbols".

## VR

### Preparation

1. Open "File" -> "Build Settings...".
2. Change platform to "Android".
3. Set "Texture Compression" to "ATSC".
4. Push "Player Settings..." button.
5. Select "Player".
6. Expand "Other Settings".
7. Add "P56" and "OVR" symbol to "Scripting Defines Symbols".
8. Run "store_files.bat" in "ForP56" folder.

### Import of Oculus Integration

1. Import "Oculus Integration" on "Package Manager".

### Install of XR Plugin Manager

1. Open "Edit" -> "Project Settings..." -> "XR Plugin Management".
2. Push "Install XR Plugin Management" button.
3. Change to "Android settins" tab.
4. Check "Oculus" checkbox.

### Quality Setting

1. Open "Edit" -> "Project Settings..." -> "Quality".
2. Push "Add Quality Level" button and Select "Oculus".
3. Change "Name" of "Current Active Quality Level" to "Oculus".
4. Change settings by reference to "quality_oculus.png" file.

### Build

1. Open "File" -> "Build Settings...".
2. Push "Build And Run" button".
