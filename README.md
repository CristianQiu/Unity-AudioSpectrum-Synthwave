# About
Audio visualization based on third octave bands: https://en.wikipedia.org/wiki/Octave_band.
The physical scene is almost completely procedural, only buildings and text have been placed manually.

# Details
  - Made with Universal Render Pipeline (URP).
  - Custom unlit shaders with smoothstep and partial derivatives to apply antialiasing to some of the stuff like the grid lines and the sun. https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-fwidth.
  - Sky and sun color are basically a gradient with three points. The mid point is animated to fake sunrise and sunset.
  - The grid mesh vertices are animated using the job system and noise, along with the audio data. It uses the new mesh API that I believe was introduced somewhere between the 2019-2020 cycle. For more info see https://github.com/Unity-Technologies/MeshApiExamples.
  - The buildings windows are just an "inverted" grid. Noise is used to turn on and off the window lights based on some beats.
  - I also wanted to carry the demo on my Android mobile (Pixel 3a - Snapdragon 670) so I made it mobile friendly, while keeping the maximum quality I could.

# Additional info
Some of the values are animated to match the mood of the testing music used. Although you can still swap the audio file to visualize it the scene animations may glitch depending on the length of the clip. You can click the left button (or tap the screen in Android) to see the debug bars in the UI.

# License
