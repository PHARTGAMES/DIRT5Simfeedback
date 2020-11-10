# DIRT5Simfeedback


ABOUT
=====
DIRT5 Plugin for motion telemetry through SimFeedback.

https://opensfx.com/

https://github.com/SimFeedback/SimFeedback-AC-Servo


RELEASE NOTES
=============
v1.0 - First release.


INSTALLATION INSTRUCTIONS 
=========================

1. Ensure you have the .NET Framework v4.8 runtime installed.

Download: https://dotnet.microsoft.com/download/dotnet-framework/net48

2. Download and extract the latest release zip of DIRT5Simfeedback.

Download: https://github.com/PHARTGAMES/DIRT5Simfeedback/tree/master/Releases

3. Copy the contents of the Dirt5TelemetryProvider folder within the Dirt5Simfeedback .zip into your SimFeedback root folder.


USAGE INSTRUCTIONS 
==================

1. Launch DIRT5; currently this plugin has only been tested with the steam version.

2. Once on the main menu of DIRT5, launch the Dirt5MatrixProvider.exe that is inside the Dirt5MatrixProvider folder.

3. In Dirt5MatrixProvider, using the combo box on the left, select the vehicle that matches the currently selected/visible vehicle on the main menu. If you can't figure out which vehicle you are using, you
can setup an Arcade event, get to the stage where the vehicle selection occurs. Highlight a vehicle you can match by name to a vehicle name in the combo box. (some of them are a touch obscure) aston_gt4 and
skoda_fabia_r5 are easy ones.

4. Once you know you have a match, click the "Initialize!" button and wait for Dirt5MatrixProvider to find the matrix.

5. If it finds the correct matrix the status string will change from "Please Wait" to "Success!" and the live data for the found matrix will be shown in the text box.

   If it fails, it's probably that you don't have a match for the selected car, or something has changed in the executable that makes this plugin no longer function. Seek help in #Dirt5 on the SimFeedback owner's Discord.

   NOTE: You must leave Dirt5MatrixProvider running for the duration of your game session as it provides the matrix to the Dirt5TelemetryProvider plugin.

7. In Simfeedback, activate the Dirt5 profile you wish to use and click the Start button.

8. From this point you can choose any car and track and you will get motion.


NOTES:

IF you close Dirt5 you need to start again at step 1.

IF you close Simfeedback you need to start at step 6.

IF you close Dirt5MatrixProvider you need to start at step 2 and reload Simfeedback.

IF you stop the profile within Simfeedback you don't need to do anything besides start the profile again.


AUTHOR
======

PEZZALUCIFER


SUPPORT
=======

Support available through SimFeedback owner's discord

https://opensfx.com/simfeedback-setup-and-tuning/#modes
