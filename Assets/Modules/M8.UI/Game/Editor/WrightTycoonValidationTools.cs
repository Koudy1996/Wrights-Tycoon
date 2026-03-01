using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WrightTycoon.Game.RuntimeData;
using WrightTycoon.Game.Save;

namespace WrightTycoon.Game.Editor;

public static class WrightTycoonValidationTools
{
    [MenuItem("WrightTycoon/Validate/Runtime Data Root")]
    public static void ValidateRuntimeDataRoot()
    {
        var playerInfoRoot = WrightTycoonRuntimePaths.GetPlayerInfoRootPath();
        if (Directory.Exists(playerInfoRoot))
        {
            Directory.Delete(playerInfoRoot, true);
        }

        WrightTycoonRuntimePaths.EnsurePlayerInfoLayout();
        var recreatedRoot = Directory.Exists(playerInfoRoot);

        var savePath = WrightTycoonRuntimePaths.GetSaveFilePath("saves.json");
        var saveService = new GameSavePersistenceService(savePath);
        saveService.CreateManualSave("Validation Save", 0, "Validation Co", "{}");
        var saveCreated = File.Exists(savePath);
        var saveRoundTripLoaded = new GameSavePersistenceService(savePath)
            .GetOrderedForLoadMenu()
            .Count == 1;

        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }

        var emptyAfterSaveDelete = new GameSavePersistenceService(savePath)
            .GetOrderedForLoadMenu()
            .Count == 0;

        var generatedManifestPath = WrightTycoonBuildManifestService.GetRuntimeCachePath();
        if (File.Exists(generatedManifestPath))
        {
            File.Delete(generatedManifestPath);
        }

        var manifest = WrightTycoonBuildManifestService.LoadOrCreateRuntimeManifest();
        var generatedAssetRecreated = File.Exists(generatedManifestPath);

        var logsDirectory = WrightTycoonRuntimePaths.GetLogsDirectory();
        if (Directory.Exists(logsDirectory))
        {
            Directory.Delete(logsDirectory, true);
        }

        WrightTycoonRuntimeLogSink.EnsureInitializedForCurrentProcess();
        WrightTycoonRuntimeLogSink.WriteDiagnostic("Runtime data validation");

        var logsRecreated = Directory.Exists(logsDirectory)
            && Directory.EnumerateFiles(logsDirectory).Any();

        if (!recreatedRoot || !saveCreated || !saveRoundTripLoaded || !emptyAfterSaveDelete || !generatedAssetRecreated || !logsRecreated)
        {
            throw new InvalidOperationException(
                $"Runtime data validation failed. Root={recreatedRoot}, SaveCreated={saveCreated}, SaveRoundTripLoaded={saveRoundTripLoaded}, EmptyAfterDelete={emptyAfterSaveDelete}, GeneratedAssets={generatedAssetRecreated}, Logs={logsRecreated}.");
        }

        Debug.Log(
            $"[Validation] Runtime data root OK. PlayerInfo={playerInfoRoot} | SavePath={savePath} | GeneratedManifest={generatedManifestPath} | ManifestPlatform={manifest.TargetPlatform}");
    }
}
