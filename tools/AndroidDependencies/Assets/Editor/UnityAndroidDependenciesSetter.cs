using System;
using UnityEditor;

public static class UnityAndroidDependenciesSetter {

    private static class EnvironmentVariables
    {
        public static readonly string JdkPath = Environment.GetEnvironmentVariable("JAVA_HOME");
        public static readonly string AndroidSdkPath = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        public static readonly string AndroidNdkPath = Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
    }

    private static class UnityEditorPrefKeys
    {
        public const string Jdk = "JdkPath";
        public const string AndroidSdk = "AndroidSdkRoot";
        public const string AndroidNdk = "AndroidNdkRoot";
    }

    public static void Set()
    {
        Console.WriteLine("Android preferences before execution:");
        PrintPreferences();
        
        EditorPrefs.SetString(UnityEditorPrefKeys.Jdk, EnvironmentVariables.JdkPath);
        EditorPrefs.SetString(UnityEditorPrefKeys.AndroidSdk, EnvironmentVariables.AndroidSdkPath);
        EditorPrefs.SetString(UnityEditorPrefKeys.AndroidNdk, EnvironmentVariables.AndroidNdkPath);
        
        Console.WriteLine("Android preferences after execution:");
        PrintPreferences();
    }

    private static void PrintPreferences()
    {
        Console.WriteLine($"JDK Path: {EditorPrefs.GetString(UnityEditorPrefKeys.Jdk)}");
        Console.WriteLine($"Android SDK Path: {EditorPrefs.GetString(UnityEditorPrefKeys.AndroidSdk)}");
        Console.WriteLine($"Android NDK Path: {EditorPrefs.GetString(UnityEditorPrefKeys.AndroidNdk)}");
        Console.WriteLine();
    }
}
