# Polyretina VR

This projects simulates the vision provided by a POLYRETINA epi-retinal prosthetic device using Unity. Simulations can be viewed on both a computer screen and in virtual reality (using either FOVE or VIVE Pro Eye head-mounted displays).

## Quick Setup

The project can either be forked or downloaded and used directly or can be imported into your own Unity project as an asset package (see Releases). It has currently only been tested using Unity version: 2019.2.16f1.

1. Click on the Polyretina dropdown menu -> Settings.
   - Select the VR headsets you want to use (if any). If you do not plan to use a certain VR headset you can also delete it's associated scripts (FoveUnityPlugin for the FOVE and ViveSR for the VIVE Pro Eye).
   - Select the "VRInput Support" checkbox if you are using the VIVE Pro Eye for controller input support.

### If viewing the simulation on a computer screen:

2. Make "Virtual Reality Supported" is disabled in Unity's XR Settings.
3. In the game tab, create a new resolution to match the output of the simulation (if unsure, use 2036x2260).
4. Open one of the demo scenes (Assets/Polyretina/SPV/Demos/), such as Objects.
5. Ensure the "SRanipal Eye Framework" GameObject is disabled.
6. Run the simulation.

### If viewing in the VIVE Pro Eye:

7. Make "Virtual Reality Supported" is enabled in Unity's XR Settings.
8. Open one of the demo scenes (Assets/Polyretina/SPV/Demos/), such as Objects.
9. Ensure the "SRanipal Eye Framework" GameObject is enabled.
10. Run the simulation.
11. Select the "Remote" resolution from the game tab for accurate viewing in the Unity Editor.

## In-Depth Description

Coming soon...
