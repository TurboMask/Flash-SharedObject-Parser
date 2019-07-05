# Flash SharedObject Parser

Here is a C# class for parsing Flash (ActionScript 3) SharedObject data. Useful when porting AIR apps to Unity or other C#-based engine.
Only primitive types are implemented: bool, int, double, string.

### Usage

General use case:
1. User installs your AIR app. App writes some data to SharedObject, like this:
   ```
   var so:SharedObject = SharedObject.getLocal("saved_data");
   so.data["int_param"] = 12;
   so.flush();
   ```
2. You port your app to Unity, integrate `SharedObjectParser` to read saved data in Unity.
3. User updates app. Now it's Unity, not AIR. App successfully reads shared object saved by AIR so user's progress is not lost.

File Test.cs contains example of how this class can be used in Unity for Android app.

### File paths

AIR and Unity use different directories for saving data:
- On Android:
  - AIR (`File.applicationStorageDirectory`): `/data/user/0/[bundle ID]/[bundle ID]/Local Store/`
  - Unity (`Application.persistentDataPath`): `/sdcard/Android/data/[bundle ID]/`
- On Amazon:
  - AIR (`File.applicationStorageDirectory`): `/data/data/[bundle ID]/[bundle ID]/Local Store/`
  - Unity (`Application.persistentDataPath`): `/sdcard/Android/data/[bundle ID]/`
- On iOS:
  - AIR (`File.applicationStorageDirectory`): `/var/mobile/Containers/Data/Application/[app code]/Library/Application Support/[bundle ID]/Local Store/`
  - Unity (`Application.persistentDataPath`): `/var/mobile/Containers/Data/Application/[app code]/Documents/`

For Android and Amazon you need to use full paths and for iOS you just go one directory up and then attach other formalities, like this:
`Directory.GetParent(Application.persistentDataPath).ToString() + "/Library/Application Support/" + bundle_id + "/Local Store/"`.

SharedObject files are saved to directory `#SharedObjects` and have extension `.sol`. So the full path to SharedObject file on Android looks like this:
`/data/user/0/[bundle ID]/[bundle ID]/Local Store/#SharedObjects/[so name].sol`. You need to pass this path to `SharedObjectParser.Parse()`.

Other files saved by AIR app can be accessed the same way.

### Other hints for porting AIR to Unity

##### Test app updates

To test if new app version is reading old data correctly you need to install new app as update. This can be achieved by uninstalling app with ADB with option to keep files:
`adb shell pm uninstall -k [bundle ID]`. Then install new app version as usual. Also make sure bundle ID is the same.

##### Convert certificates

To publish your ported app to app store it needs to be signed with the same certificate as previous (AIR) version. It's straightforward for iOS, but for Android and Amazon you'll possibly need to convert certificate to other format which is readable for Unity. This command does the job:
`keytool -importkeystore -srckeystore [old cert name].p12 -srcstoretype pkcs12 -srcalias [alias] -destkeystore [new cert name].keystore -deststoretype jks -deststorepass [password] -destalias [alias]`. For `alias` you can just use `1`, but in general it depends on what data is in your source certificate.