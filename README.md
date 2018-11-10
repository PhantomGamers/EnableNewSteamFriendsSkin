# EnableNewSteamFriendsSkin
This utility will automatically enable skin support on the new Steam Friends UI.

# Instructions
Simply extract the exe anywhere and run it, it will automatically find and modify your cached friends.css file so that it is ready for skinning.
Put your custom css in your Steam folder's clientui subfolder under the file name "friends.custom.css"
When Valve updates the friends.css file you will need to rerun this program to reenable your skin.

# Launch arguments
You can launch this program with the following arguments:  

* -p="" or --pass=""  
This lets you pass arguments to Steam through this program when the program has to launch Steam on its own (either when used as a launcher or when restarting Steam.)  
Example:  
-p="-dev"  
Will launch Steam in developer mode.  
--pass="-dev -tcp"  
Will launch Steam in developer mode and force a TCP connection.  

* -s or --silent  
This will run the program in the background without a window, if Steam is not running it will automatically start Steam for you.  
This can be used to ensure you always have your custom CSS enabled.  

* -sp="" or --steampath=""  
This will override the program's detection of the Steam path, useful for if your Steam is not properly installed (E.g. if it's being ran from a flash drive)  
Note: If using backslashes you must use two otherwise it will not work.  
Example:  
-sp="C:/Program Files (x86)/Steam/"  
--steampath="C:\\\Program Files (x86)\\\Steam\\\\"  

* -sl="" or --steamlang=""  
This will override the program's detection of the Steam language, same use scenario as above.  
Note: You must use the language name associated with your translation file.   
If you're having difficulty check the file names in your Steam directory's "friends" folder. They will be named tracker_YOURLANGUAGEHERE.txt.  
Example:  
-sl="english"  
--steamlang="english"

# Todo/Bugs
* Implement CEF index parsing to better find the correct cache file (Pull Request welcome to anyone who has experience with this)

# Dependencies
* .NET Framework 4.6

# Credits
* Darth from the Steam community forums for the method.
* @henrikx for Steam directory detection code.
* Sam Allen of Dot Net Perls for GZIP compression, decompression, and detection code.
* Bob Learned of Experts Exchange for FindWindowLike code.
