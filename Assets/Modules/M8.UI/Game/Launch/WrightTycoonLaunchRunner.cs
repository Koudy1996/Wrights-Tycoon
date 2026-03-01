using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Research;
using WrightTycoon.Game.GovernmentContracts;
using WrightTycoon.Game.RuntimeData;
using WrightTycoon.Game.UI;
using WrightTycoon.Game.Save;

namespace WrightTycoon.Game.Launch;

public sealed class WrightTycoonLaunchRunner : MonoBehaviour
{
    private const int FixedFactoryToolRequirementLevel = 1;

    [Header("Default Definition IDs")]
    [SerializeField] private string _engineDefinitionId = "eng.radial.main";
    [SerializeField] private string _airframeDefinitionId = "air.medium.main";

    [Header("Family Defaults")]
    [SerializeField] private int _defaultEngineCount = 1;
    [SerializeField] private double _baseDevelopmentCost = 92000d;
    [SerializeField] private double _baseDevelopmentManDays = 240d;

    private readonly int[] _engineCountOptions = { 1, 2, 3, 4, 5, 6 };
    private readonly string[] _tabs = { "Engine Designer", "Airframe + Type Designer", "Research", "Factory", "Contracts", "Government Contracts", "Finance" };
    private readonly string[] _financeSubTabs = { "Income / Expense", "Last Month Details" };
    private readonly string[] _researchSubTabs = { "Current Research", "For Approval" };
    private readonly IsaAtmosphereModel _atmosphereModel = new();
    private readonly AerodynamicsCalculator _aerodynamicsCalculator = new();
    private readonly CategoryComplianceEvaluator _categoryComplianceEvaluator = new();
    private readonly List<SavedEngineDesign> _savedEngines = new();
    private readonly List<SavedFamilyDesign> _savedFamilies = new();
    private readonly List<SavedAircraftDesign> _savedAircraftDesigns = new();
    private readonly List<Contract> _lastGeneratedContracts = new();
    private readonly Dictionary<string, string> _selectedWarehouseDesignByContractId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _deliveredContractProfitById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeResearchModifierPoints> _researchModifiersByResearchId = new(StringComparer.Ordinal);

    private readonly FactoryAccountingCalculator _factoryAccountingCalculator = new();
    private readonly ContractPricingCalculator _contractPricingCalculator = new();
    private readonly ResearchCalculator _researchCalculator = new();
    private readonly AircraftPreview3DRenderer _aircraftPreview3DRenderer = new();

    private EngineBuilder? _engineBuilder;
    private AirframeBuilder? _airframeBuilder;
    private AircraftFactory? _aircraftFactory;
    private ContractBoard? _contractBoard;
    private ContractWarehouseDeliveryService? _contractWarehouseDeliveryService;
    private GovernmentContractBoard? _governmentContractBoard;
    private IGovernmentContractInventoryGateway? _governmentInventoryGateway;
    private AircraftProductionFactory? _productionFactory;
    private AccountingSystem? _accountingSystem;
    private ResearchBoard? _researchBoard;
    private EngineDefinitionRepository? _engineRepo;
    private AirframeDefinitionRepository? _airframeRepo;

    private List<EngineDefinition> _engineDefinitions = new();
    private List<AirframeDefinition> _airframeDefinitions = new();

    private EngineParameters _engineParameters = new();
    private AirframeParameters _baseAirframeParameters = new();

    private Engine? _enginePreview;
    private Aircraft? _aircraftPreview;
    private FactoryMonthlyProductionReport? _lastFactoryReport;
    private string _enginePreviewError = string.Empty;
    private string _aircraftPreviewError = string.Empty;
    private string _contractActionMessage = string.Empty;
    private string _governmentContractActionMessage = string.Empty;
    private string _governmentDeliveryActionMessage = string.Empty;
    private GovernmentContractNotebookWindow? _governmentContractNotebookWindow;
    private ContractUIState _governmentContractNotebookState = CreateDefaultGovernmentContractNotebookState();
    private string _factoryActionMessage = string.Empty;
    private string _researchActionMessage = string.Empty;
    private string _initError = string.Empty;

    private int _tab;
    private int _financeSubTab;
    private int _researchSubTab;
    private int _selectedEngineDefinitionIndex;
    private int _selectedAirframeDefinitionIndex;
    private int _selectedSavedEngineIndex = -1;
    private int _selectedSavedFamilyIndex = -1;
    private int _selectedSavedAircraftDesignIndex = -1;
    private int _lockedEngineCount = 1;
    private EngineDesignerFamily _engineDesignerFamily = EngineDesignerFamily.Piston;
    private AircraftAllowedEngineFamily _familyAllowedEngineFamily = AircraftAllowedEngineFamily.Propeller;
    private EngineType _selectedPistonArrangement = EngineType.Radial;
    private EngineType _selectedJetTurbineType = EngineType.Turbofan;
    private StructureType _lockedStructure = StructureType.Monocoque;
    private MaterialType _lockedMaterial = MaterialType.Aluminum;
    private AircraftCategory _selectedAircraftCategory = AircraftCategory.LightSportAndRecreational;

    private string _engineDesignName = "Prototype Engine";
    private string _aircraftDesignName = "Prototype Aircraft";
    private string _familyName = "Player Family";
    private string _typeName = "Player Variant";
    private int _requiredLineToolLevel = 1;
    private int _typeCrewCount = 2;
    private TailType _typeTailType = TailType.Classic;
    private WingShape _typeWingShape = WingShape.Straight;
    private WingProfileType _typeWingProfile = WingProfileType.Laminar6SeriesAerodynamic;
    private double _typeWingAreaSquareMeters = 36d;
    private FlapType _typeFlapType = FlapType.Fowler;
    private bool _typeHasLeadingEdgeFlaps;
    private bool _typeHasBackupSystems;
    private LandingGearType _typeLandingGearType = LandingGearType.SemiRetractable;
    private RunwayType _typeRunwayType = RunwayType.Medium;
    private double _typeCowling = 0.55d;
    private double _typeAerodynamicCleanlinessPercent;
    private double _typeFuelLiters = 1500d;
    private SimulationDate _currentDate = SimulationDate.WrightTycoonStart;
    private int _elapsedMonths;
    private ContractDeliverabilityFilter _contractDeliverabilityFilter = ContractDeliverabilityFilter.All;
    private ContractSpecificationFilter _contractSpecificationFilter = ContractSpecificationFilter.All;
    private int _contractQuantityFilterMin = 5;
    private int _contractQuantityFilterMax = 500;

    private bool _initialized;
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;
    private readonly Dictionary<string, string> _doubleSliderInputBuffer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _intSliderInputBuffer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _doubleSliderHadFocus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _intSliderHadFocus = new(StringComparer.Ordinal);

    private const string IntroBackgroundResourcePath = "WT04/Menu/main_menu_background";
    private const string SaveFileName = "saves.json";
    private const int AutosaveIntervalMonths = 6;
    private const int AutosaveRetentionCount = 4;
    private const int SavePayloadVersion = 1;

    private readonly JsonSerializerSettings _saveJsonSettings = CreateSaveJsonSettings();
    private static readonly ResolutionOption[] CommonResolutionOptions =
    {
        new(1280, 720),
        new(1280, 800),
        new(1366, 768),
        new(1440, 900),
        new(1600, 900),
        new(1680, 1050),
        new(1920, 1080),
        new(1920, 1200),
        new(2560, 1440),
        new(2560, 1600),
        new(2880, 1800),
        new(3200, 1800),
        new(3840, 2160),
    };

    private GameSavePersistenceService? _savePersistence;
    private WrightTycoonBuildManifest _buildManifest = new();
    private Texture2D? _introBackgroundTexture;
    private RuntimeScreen _runtimeScreen = RuntimeScreen.Intro;
    private Vector2 _loadMenuScroll;
    private string _saveActionMessage = string.Empty;
    private string _playerCompanyName = "Player Company";
    private readonly List<ResolutionOption> _availableResolutionOptions = new();
    private string[] _availableResolutionLabels = Array.Empty<string>();
    private int _selectedResolutionOptionIndex;
    private string _optionsActionMessage = string.Empty;

    private void Awake()
    {
        InitializeSaveSystem();
        LoadBuildManifest();
        LoadIntroBackgroundTexture();
        TryInitialize();
    }

    private void OnDestroy()
    {
        _aircraftPreview3DRenderer.Dispose();
    }

    private void Update()
    {
        if (_governmentContractNotebookWindow is null)
        {
            return;
        }

        _governmentContractNotebookState = CloneGovernmentContractNotebookState(_governmentContractNotebookWindow.CaptureState());
    }

    private void OnGUI()
    {
        using var guiScale = WrightTycoonGuiScaling.Begin();
        var virtualScreenSize = guiScale.VirtualScreenSize;

        if (_runtimeScreen == RuntimeScreen.Intro)
        {
            DrawIntroScreen(virtualScreenSize);
            return;
        }

        if (_runtimeScreen == RuntimeScreen.LoadMenu)
        {
            DrawLoadMenuScreen(virtualScreenSize);
            return;
        }

        if (_runtimeScreen == RuntimeScreen.OptionsMenu)
        {
            DrawOptionsScreen(virtualScreenSize);
            return;
        }

        var root = new Rect(10f, 10f, virtualScreenSize.x - 20f, virtualScreenSize.y - 20f);
        DrawRect(root, new Color(0.08f, 0.12f, 0.18f, 0.98f));

        GUILayout.BeginArea(root);
        GUILayout.Label("Wright Tycoon Designer");
        GUILayout.Label("Live tuning UI for M2 engines and M3/M4 airframe+type composition.");

        if (!_initialized)
        {
            if (!string.IsNullOrWhiteSpace(_initError))
            {
                DrawError(_initError);
                if (GUILayout.Button("Retry Initialization", GUILayout.Height(30f)))
                {
                    _initError = string.Empty;
                    _initialized = false;
                    TryInitialize();
                }
            }
            else
            {
                GUILayout.Label("Initializing...");
            }

            GUILayout.EndArea();
            return;
        }

        _tab = GUILayout.Toolbar(_tab, _tabs, GUILayout.Height(34f));
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label($"Date: {FormatSimulationDate(_currentDate)}", GUILayout.Width(220f));
        GUILayout.Label($"Company: {_playerCompanyName}", GUILayout.Width(220f));
        if (GUILayout.Button("Next Month", GUILayout.Width(150f), GUILayout.Height(26f)))
        {
            AdvanceToNextMonth();
        }

        if (GUILayout.Button("Save", GUILayout.Width(110f), GUILayout.Height(26f)))
        {
            CreateManualSave();
        }

        if (GUILayout.Button("Main Menu", GUILayout.Width(120f), GUILayout.Height(26f)))
        {
            _runtimeScreen = RuntimeScreen.Intro;
            CloseGovernmentContractNotebookWindow();
        }

        GUILayout.FlexibleSpace();
        var cash = _accountingSystem?.Cash ?? 0d;
        DrawSignedCurrencyInline($"Cash: {FormatCurrency(cash)}", cash, 220f);
        GUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(_saveActionMessage))
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(_saveActionMessage);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);

        GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

        var leftWidth = Mathf.Max(430f, root.width * 0.44f);
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(leftWidth), GUILayout.ExpandHeight(true));
        _leftScroll = GUILayout.BeginScrollView(_leftScroll);
        if (_tab == 0)
        {
            DrawEngineTab();
        }
        else if (_tab == 1)
        {
            DrawAirframeTab();
        }
        else if (_tab == 2)
        {
            DrawResearchTab();
        }
        else if (_tab == 3)
        {
            DrawFactoryTab();
        }
        else if (_tab == 4)
        {
            DrawContractsTab();
        }
        else if (_tab == 5)
        {
            DrawGovernmentContractsTab();
        }
        else
        {
            DrawFinanceTab();
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        _rightScroll = GUILayout.BeginScrollView(_rightScroll);
        if (_tab == 0)
        {
            DrawEngineReport();
        }
        else if (_tab == 1)
        {
            DrawAirframeReport();
        }
        else if (_tab == 2)
        {
            DrawResearchReport();
        }
        else if (_tab == 3)
        {
            DrawFactoryReport();
        }
        else if (_tab == 4)
        {
            DrawContractsReport();
        }
        else if (_tab == 5)
        {
            DrawGovernmentContractsReport();
        }
        else
        {
            DrawFinanceReport();
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void TryInitialize()
    {
        try
        {
            if (_initialized)
            {
                return;
            }

            InitializeCoreSystems();
            ResetRuntimeStateForNewGame();
            _initialized = true;
        }
        catch (Exception ex)
        {
            _initError = ex.ToString();
            Debug.LogError(_initError);
        }
    }

    private void InitializeCoreSystems()
    {
        if (_engineRepo is not null
            && _airframeRepo is not null
            && _engineBuilder is not null
            && _airframeBuilder is not null
            && _aircraftFactory is not null
            && _engineDefinitions.Count > 0
            && _airframeDefinitions.Count > 0)
        {
            return;
        }

        var paths = ResolveDefinitionPaths();
        _engineRepo = new JsonEngineDefinitionRepository().LoadFromFile(paths.EngineJsonPath);
        _airframeRepo = new JsonAirframeDefinitionRepository().LoadFromFile(paths.AirframeJsonPath);

        _engineDefinitions = _engineRepo.GetAll().OrderBy(x => x.DisplayName).ToList();
        _airframeDefinitions = _airframeRepo.GetAll().OrderBy(x => x.DisplayName).ToList();

        if (_engineDefinitions.Count == 0 || _airframeDefinitions.Count == 0)
        {
            throw new InvalidOperationException("No engine/airframe definitions found.");
        }

        _engineBuilder = new EngineBuilder(new EngineCalculator(), _atmosphereModel);
        _airframeBuilder = new AirframeBuilder(new AirframeCalculator(_aerodynamicsCalculator, _atmosphereModel));
        _aircraftFactory = new AircraftFactory(
            _engineBuilder,
            _airframeBuilder,
            new AircraftPerformanceCalculator(_atmosphereModel, _aerodynamicsCalculator),
            new DevelopmentCostCalculator(),
            _engineRepo,
            _airframeRepo);
    }

    private void ResetRuntimeStateForNewGame()
    {
        _selectedEngineDefinitionIndex = FindIndexById(_engineDefinitions.Select(d => d.Id).ToArray(), _engineDefinitionId);
        _selectedAirframeDefinitionIndex = FindIndexById(_airframeDefinitions.Select(d => d.Id).ToArray(), _airframeDefinitionId);
        _lockedEngineCount = NormalizeEngineCount(_defaultEngineCount);
        SyncEngineSelectorsFromDefinition(_engineDefinitions[_selectedEngineDefinitionIndex].EngineType);

        _engineParameters = CreateDefaultEngineParameters(_engineDefinitions[_selectedEngineDefinitionIndex]);
        _baseAirframeParameters = CreateDefaultAirframeParameters(_lockedEngineCount);
        SyncTypeAirframeFromBase();
        RebuildEnginePreview();

        _contractBoard = new ContractBoard(new ContractGenerator(), new ContractEvaluator());
        _productionFactory = new AircraftProductionFactory();
        _contractWarehouseDeliveryService = new ContractWarehouseDeliveryService(_contractBoard, _productionFactory);
        var governmentRandom = new SystemGovernmentContractRandomProvider();
        var governmentGenerator = new GovernmentContractGenerator(governmentRandom);
        _governmentContractBoard = new GovernmentContractBoard(governmentGenerator, governmentRandom);
        _governmentInventoryGateway = new FactoryGovernmentContractInventoryGateway(_productionFactory);
        _accountingSystem = new AccountingSystem(AccountingDefaults.StartingCash);
        _researchBoard = new ResearchBoard();

        _lastGeneratedContracts.Clear();
        _savedEngines.Clear();
        _savedFamilies.Clear();
        _savedAircraftDesigns.Clear();
        _researchModifiersByResearchId.Clear();
        _selectedWarehouseDesignByContractId.Clear();
        _deliveredContractProfitById.Clear();
        _selectedSavedEngineIndex = -1;
        _selectedSavedFamilyIndex = -1;
        _selectedSavedAircraftDesignIndex = -1;
        _lastFactoryReport = null;
        _currentDate = SimulationDate.WrightTycoonStart;
        _elapsedMonths = 0;
        _tab = 0;
        _financeSubTab = 0;
        _researchSubTab = 0;
        _contractActionMessage = string.Empty;
        _governmentContractActionMessage = string.Empty;
        _governmentDeliveryActionMessage = string.Empty;
        _factoryActionMessage = string.Empty;
        _researchActionMessage = string.Empty;
        _saveActionMessage = string.Empty;
        _contractDeliverabilityFilter = ContractDeliverabilityFilter.All;
        _contractSpecificationFilter = ContractSpecificationFilter.All;
        _contractQuantityFilterMin = 5;
        _contractQuantityFilterMax = 500;

        if (_governmentContractBoard is not null)
        {
            var initialGovernmentUpdate = _governmentContractBoard.ProcessMonth(_currentDate);
            if (initialGovernmentUpdate.Events.Count > 0)
            {
                _governmentContractActionMessage = string.Join(" ", initialGovernmentUpdate.Events);
            }
        }

        ResetGovernmentContractNotebookWindow();
    }

    private void InitializeSaveSystem()
    {
        WrightTycoonRuntimePaths.EnsurePlayerInfoLayout();
        var savePath = WrightTycoonRuntimePaths.GetSaveFilePath(SaveFileName);
        _savePersistence = new GameSavePersistenceService(savePath, _saveJsonSettings);
        Debug.Log($"[RuntimeData] Player Info root: {WrightTycoonRuntimePaths.GetPlayerInfoRootPath()}");
        Debug.Log($"[Save] Using save file: {savePath}");
    }

    private void LoadIntroBackgroundTexture()
    {
        _introBackgroundTexture = Resources.Load<Texture2D>(IntroBackgroundResourcePath);
        if (_introBackgroundTexture is null)
        {
            Debug.LogWarning($"[Menu] Intro background not found in Resources at '{IntroBackgroundResourcePath}'.");
        }
    }

    private void LoadBuildManifest()
    {
        _buildManifest = WrightTycoonBuildManifestService.LoadOrCreateRuntimeManifest();
    }

    private void DrawIntroScreen(Vector2 virtualScreenSize)
    {
        DrawIntroBackground(virtualScreenSize);

        var panelWidth = Mathf.Min(560f, virtualScreenSize.x - 40f);
        var panelHeight = Mathf.Min(496f, virtualScreenSize.y - 40f);
        var panelRect = new Rect(
            (virtualScreenSize.x - panelWidth) * 0.5f,
            (virtualScreenSize.y - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        var hasContinueSave = _savePersistence is not null && _savePersistence.TryGetContinueEntry(out _);

        GUILayout.BeginArea(panelRect, GUI.skin.box);
        GUILayout.Label("Wright Tycoon");
        GUILayout.Label("Main Menu");
        DrawBuildManifestSummary();

        _playerCompanyName = DrawText("Company Name", _playerCompanyName);
        if (string.IsNullOrWhiteSpace(_playerCompanyName))
        {
            _playerCompanyName = "Player Company";
        }

        GUILayout.Space(10f);

        var previousEnabled = GUI.enabled;
        GUI.enabled = hasContinueSave;
        if (GUILayout.Button("Continue", GUILayout.Height(44f)))
        {
            ContinueLastPlayedSave();
        }

        GUI.enabled = previousEnabled;

        if (GUILayout.Button("New Game", GUILayout.Height(44f)))
        {
            StartNewGameFromMenu();
        }

        if (GUILayout.Button("Load Game", GUILayout.Height(44f)))
        {
            _runtimeScreen = RuntimeScreen.LoadMenu;
        }

        if (GUILayout.Button("Options", GUILayout.Height(44f)))
        {
            OpenOptionsMenu();
        }

        if (GUILayout.Button("Exit", GUILayout.Height(44f)))
        {
            Application.Quit();
        }

        if (!_initialized && !string.IsNullOrWhiteSpace(_initError))
        {
            GUILayout.Space(8f);
            DrawError("Initialization failed. Use Retry Initialization in gameplay view or fix project data paths.");
        }
        else if (!hasContinueSave)
        {
            GUILayout.Space(8f);
            GUILayout.Label("No save available to continue.");
        }

        GUILayout.EndArea();
    }

    private void DrawBuildManifestSummary()
    {
        var gameVersion = string.IsNullOrWhiteSpace(_buildManifest.GameVersion)
            ? "0.0.0"
            : _buildManifest.GameVersion;
        var targetPlatform = string.IsNullOrWhiteSpace(_buildManifest.TargetPlatform)
            ? Application.platform.ToString()
            : _buildManifest.TargetPlatform;

        GUILayout.Label($"Version: {gameVersion} | Platform: {targetPlatform}");

        var buildLine = $"Build: {FormatBuildManifestTimestamp(_buildManifest.BuildTimestampUtc)}";
        if (!string.IsNullOrWhiteSpace(_buildManifest.GitCommit))
        {
            buildLine += $" | Git: {_buildManifest.GitCommit}";
        }

        GUILayout.Label(buildLine);

        if (!string.IsNullOrWhiteSpace(_buildManifest.BalanceChecksum))
        {
            var checksum = _buildManifest.BalanceChecksum.Length > 8
                ? _buildManifest.BalanceChecksum[..8]
                : _buildManifest.BalanceChecksum;

            var balanceLine = _buildManifest.BalanceFileCount > 0
                ? $"Balance: {checksum} ({_buildManifest.BalanceFileCount} files)"
                : $"Balance: {checksum}";

            GUILayout.Label(balanceLine);
        }

        GUILayout.Space(8f);
    }

    private void DrawLoadMenuScreen(Vector2 virtualScreenSize)
    {
        DrawIntroBackground(virtualScreenSize);

        var root = new Rect(20f, 20f, virtualScreenSize.x - 40f, virtualScreenSize.y - 40f);
        DrawRect(root, new Color(0.06f, 0.09f, 0.14f, 0.9f));

        GUILayout.BeginArea(root);
        GUILayout.Label("Load Game");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Back", GUILayout.Width(180f), GUILayout.Height(44f)))
        {
            _runtimeScreen = RuntimeScreen.Intro;
        }

        if (GUILayout.Button("Exit", GUILayout.Width(180f), GUILayout.Height(44f)))
        {
            Application.Quit();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (_savePersistence is null)
        {
            DrawError("Save system is not initialized.");
            GUILayout.EndArea();
            return;
        }

        var entries = _savePersistence.GetOrderedForLoadMenu();
        if (entries.Count == 0)
        {
            GUILayout.Label("No saves found.");
            GUILayout.EndArea();
            return;
        }

        _loadMenuScroll = GUILayout.BeginScrollView(_loadMenuScroll);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            GUILayout.BeginHorizontal(GUI.skin.box);

            var typeLabel = string.Equals(entry.Type, "autosave", StringComparison.OrdinalIgnoreCase) ? "AUTOSAVE" : "MANUAL";
            GUILayout.Label($"[{typeLabel}] {entry.SlotName}", GUILayout.Width(420f));
            GUILayout.Label($"Created: {entry.CreatedAtIso}", GUILayout.Width(200f));
            GUILayout.Label($"Month: {FormatUiInteger(entry.GameMonth)}", GUILayout.Width(120f));

            if (GUILayout.Button("Load", GUILayout.Width(100f), GUILayout.Height(24f)))
            {
                LoadFromSaveEntry(entry);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawOptionsScreen(Vector2 virtualScreenSize)
    {
        DrawIntroBackground(virtualScreenSize);
        if (_availableResolutionLabels.Length == 0)
        {
            RefreshResolutionOptions();
        }

        var panelWidth = Mathf.Min(760f, virtualScreenSize.x - 40f);
        var panelHeight = Mathf.Min(620f, virtualScreenSize.y - 40f);
        var panelRect = new Rect(
            (virtualScreenSize.x - panelWidth) * 0.5f,
            (virtualScreenSize.y - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        GUILayout.BeginArea(panelRect, GUI.skin.box);
        GUILayout.Label("Options");
        GUILayout.Space(4f);
        GUILayout.Label($"Current Resolution: {Screen.width} x {Screen.height}");
        GUILayout.Label("Select a resolution and apply it. Text stays at 16 px or larger.");
        GUILayout.Space(10f);

        if (_availableResolutionLabels.Length > 0)
        {
            var columns = _availableResolutionLabels.Length >= 8 ? 2 : 1;
            _selectedResolutionOptionIndex = GUILayout.SelectionGrid(
                Mathf.Clamp(_selectedResolutionOptionIndex, 0, _availableResolutionLabels.Length - 1),
                _availableResolutionLabels,
                columns);
        }
        else
        {
            GUILayout.Label("No supported resolutions were detected.");
        }

        GUILayout.Space(12f);

        if (GUILayout.Button("Apply Resolution", GUILayout.Height(44f)))
        {
            ApplySelectedResolution();
        }

        if (GUILayout.Button("Back", GUILayout.Height(44f)))
        {
            _runtimeScreen = RuntimeScreen.Intro;
        }

        if (!string.IsNullOrWhiteSpace(_optionsActionMessage))
        {
            GUILayout.Space(10f);
            GUILayout.Label(_optionsActionMessage);
        }

        if (Application.isEditor)
        {
            GUILayout.Space(8f);
            GUILayout.Label("In the Unity Editor, resolution requests may not change the Game view size exactly.");
        }

        GUILayout.EndArea();
    }

    private void OpenOptionsMenu()
    {
        RefreshResolutionOptions();
        _optionsActionMessage = string.Empty;
        _runtimeScreen = RuntimeScreen.OptionsMenu;
    }

    private void RefreshResolutionOptions()
    {
        var displayWidth = Math.Max(Screen.currentResolution.width, Screen.width);
        var displayHeight = Math.Max(Screen.currentResolution.height, Screen.height);

        _availableResolutionOptions.Clear();
        for (var index = 0; index < CommonResolutionOptions.Length; index++)
        {
            var option = CommonResolutionOptions[index];
            if (option.Width > displayWidth || option.Height > displayHeight)
            {
                continue;
            }

            if (_availableResolutionOptions.Any(existing => existing.Width == option.Width && existing.Height == option.Height))
            {
                continue;
            }

            _availableResolutionOptions.Add(option);
        }

        if (!_availableResolutionOptions.Any(option => option.Width == Screen.width && option.Height == Screen.height))
        {
            _availableResolutionOptions.Add(new ResolutionOption(Screen.width, Screen.height));
        }

        _availableResolutionOptions.Sort((left, right) =>
        {
            var areaCompare = (left.Width * left.Height).CompareTo(right.Width * right.Height);
            if (areaCompare != 0)
            {
                return areaCompare;
            }

            var widthCompare = left.Width.CompareTo(right.Width);
            return widthCompare != 0 ? widthCompare : left.Height.CompareTo(right.Height);
        });

        _availableResolutionLabels = _availableResolutionOptions
            .Select(option => option.Label)
            .ToArray();

        _selectedResolutionOptionIndex = GetClosestResolutionOptionIndex(Screen.width, Screen.height);
    }

    private int GetClosestResolutionOptionIndex(int width, int height)
    {
        if (_availableResolutionOptions.Count == 0)
        {
            return 0;
        }

        var bestIndex = 0;
        var bestScore = long.MaxValue;

        for (var index = 0; index < _availableResolutionOptions.Count; index++)
        {
            var option = _availableResolutionOptions[index];
            var widthDelta = Math.Abs(option.Width - width);
            var heightDelta = Math.Abs(option.Height - height);
            var score = (long)widthDelta + heightDelta;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestIndex = index;
        }

        return bestIndex;
    }

    private void ApplySelectedResolution()
    {
        if (_availableResolutionOptions.Count == 0)
        {
            _optionsActionMessage = "No resolution options are available.";
            return;
        }

        var index = Mathf.Clamp(_selectedResolutionOptionIndex, 0, _availableResolutionOptions.Count - 1);
        var option = _availableResolutionOptions[index];

        try
        {
            Screen.SetResolution(option.Width, option.Height, Screen.fullScreenMode);
            _optionsActionMessage = $"Requested resolution: {option.Label}";
            _selectedResolutionOptionIndex = index;
        }
        catch (Exception ex)
        {
            _optionsActionMessage = $"Resolution change failed: {ex.Message}";
            Debug.LogError(ex);
        }
    }

    private void DrawIntroBackground(Vector2 virtualScreenSize)
    {
        var full = new Rect(0f, 0f, virtualScreenSize.x, virtualScreenSize.y);

        if (_introBackgroundTexture is not null)
        {
            var textureWidth = _introBackgroundTexture.width <= 0 ? 1f : _introBackgroundTexture.width;
            var textureHeight = _introBackgroundTexture.height <= 0 ? 1f : _introBackgroundTexture.height;
            var scale = Mathf.Max(full.width / textureWidth, full.height / textureHeight);
            var drawWidth = textureWidth * scale;
            var drawHeight = textureHeight * scale;
            var drawRect = new Rect(
                full.x + (full.width - drawWidth) * 0.5f,
                full.y + (full.height - drawHeight) * 0.5f,
                drawWidth,
                drawHeight);

            GUI.DrawTexture(drawRect, _introBackgroundTexture, ScaleMode.StretchToFill);
        }
        else
        {
            DrawRect(full, new Color(0.11f, 0.14f, 0.18f, 1f));
        }

        DrawRect(full, new Color(0f, 0f, 0f, 0.28f));
    }

    private void StartNewGameFromMenu()
    {
        TryInitialize();
        if (!_initialized)
        {
            return;
        }

        ResetRuntimeStateForNewGame();
        _runtimeScreen = RuntimeScreen.Gameplay;
        _saveActionMessage = "New game started.";
        Debug.Log("[Save] New game started.");
    }

    private void CreateManualSave()
    {
        if (_savePersistence is null)
        {
            _saveActionMessage = "Save failed: save system not initialized.";
            return;
        }

        try
        {
            var payloadJson = JsonConvert.SerializeObject(serializeGameState(), Formatting.None, _saveJsonSettings);
            var slotName = BuildSaveSlotName();
            var entry = _savePersistence.CreateManualSave(slotName, _elapsedMonths, _playerCompanyName, payloadJson);
            _saveActionMessage = $"Saved: {entry.SlotName} (Created: {entry.CreatedAtIso})";
            Debug.Log($"[Save] Manual save created: {entry.Id}");
        }
        catch (Exception ex)
        {
            _saveActionMessage = $"Save failed: {ex.Message}";
            Debug.LogError(ex);
        }
    }

    private void CreateAutosave()
    {
        if (_savePersistence is null)
        {
            return;
        }

        try
        {
            var payloadJson = JsonConvert.SerializeObject(serializeGameState(), Formatting.None, _saveJsonSettings);
            var slotName = BuildSaveSlotName();
            var entry = _savePersistence.CreateAutosave(slotName, _elapsedMonths, _playerCompanyName, payloadJson, AutosaveRetentionCount);
            _saveActionMessage = $"Autosave: {entry.SlotName} (Created: {entry.CreatedAtIso})";
            Debug.Log($"[Save] Autosave created: {entry.Id}");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    private void MaybeCreateAutosave()
    {
        if (_elapsedMonths <= 0)
        {
            return;
        }

        if (_elapsedMonths % AutosaveIntervalMonths != 0)
        {
            return;
        }

        CreateAutosave();
    }

    private string BuildSaveSlotName()
    {
        var company = string.IsNullOrWhiteSpace(_playerCompanyName) ? "Player Company" : _playerCompanyName.Trim();
        return $"{FormatSimulationDate(_currentDate)} - {company}";
    }

    private void ContinueLastPlayedSave()
    {
        if (_savePersistence is null || !_savePersistence.TryGetContinueEntry(out var entry) || entry is null)
        {
            _saveActionMessage = "Continue failed: no save available.";
            return;
        }

        LoadFromSaveEntry(entry);
    }

    private void LoadFromSaveEntry(GameSaveSlotEntry entry)
    {
        try
        {
            TryInitialize();
            if (!_initialized)
            {
                return;
            }

            ResetRuntimeStateForNewGame();

            var payload = JsonConvert.DeserializeObject<RunnerGameSavePayload>(entry.PayloadJson, _saveJsonSettings);
            if (payload is null)
            {
                throw new InvalidDataException("Save payload is empty.");
            }

            loadGameState(payload);
            _savePersistence?.MarkAsLastPlayed(entry.Id);
            _runtimeScreen = RuntimeScreen.Gameplay;
            _saveActionMessage = $"Loaded: {entry.SlotName} (Created: {entry.CreatedAtIso})";
            Debug.Log($"[Save] Loaded save: {entry.Id}");
        }
        catch (Exception ex)
        {
            _saveActionMessage = $"Load failed: {ex.Message}";
            Debug.LogError(ex);
        }
    }

    private RunnerGameSavePayload serializeGameState()
    {
        var payload = new RunnerGameSavePayload
        {
            Version = SavePayloadVersion,
            PlayerCompanyName = _playerCompanyName,
            ElapsedMonths = _elapsedMonths,
            CurrentYear = _currentDate.Year,
            CurrentMonth = _currentDate.Month,
            Tab = _tab,
            FinanceSubTab = _financeSubTab,
            ResearchSubTab = _researchSubTab,
            SelectedEngineDefinitionIndex = _selectedEngineDefinitionIndex,
            SelectedAirframeDefinitionIndex = _selectedAirframeDefinitionIndex,
            SelectedSavedEngineIndex = _selectedSavedEngineIndex,
            SelectedSavedFamilyIndex = _selectedSavedFamilyIndex,
            SelectedSavedAircraftDesignIndex = _selectedSavedAircraftDesignIndex,
            LockedEngineCount = _lockedEngineCount,
            EngineDesignerFamily = _engineDesignerFamily,
            FamilyAllowedEngineFamily = _familyAllowedEngineFamily,
            SelectedPistonArrangement = _selectedPistonArrangement,
            SelectedJetTurbineType = _selectedJetTurbineType,
            LockedStructure = _lockedStructure,
            LockedMaterial = _lockedMaterial,
            SelectedAircraftCategory = _selectedAircraftCategory,
            EngineDesignName = _engineDesignName,
            AircraftDesignName = _aircraftDesignName,
            FamilyName = _familyName,
            TypeName = _typeName,
            RequiredLineToolLevel = _requiredLineToolLevel,
            TypeCrewCount = _typeCrewCount,
            TypeTailType = _typeTailType,
            TypeWingShape = _typeWingShape,
            TypeWingProfile = _typeWingProfile,
            TypeWingAreaSquareMeters = _typeWingAreaSquareMeters,
            TypeFlapType = _typeFlapType,
            TypeHasLeadingEdgeFlaps = _typeHasLeadingEdgeFlaps,
            TypeHasBackupSystems = _typeHasBackupSystems,
            TypeLandingGearType = _typeLandingGearType,
            TypeRunwayType = _typeRunwayType,
            TypeCowling = _typeCowling,
            TypeAerodynamicCleanlinessPercent = _typeAerodynamicCleanlinessPercent,
            TypeFuelLiters = _typeFuelLiters,
            ContractDeliverabilityFilter = _contractDeliverabilityFilter,
            ContractSpecificationFilter = _contractSpecificationFilter,
            ContractQuantityFilterMin = _contractQuantityFilterMin,
            ContractQuantityFilterMax = _contractQuantityFilterMax,
            GovernmentContractNotebookState = CreateNotebookWindowSaveState(_governmentContractNotebookState),
            EngineParameters = _engineParameters.Clone(),
            BaseAirframeParameters = _baseAirframeParameters.Clone(),
            ContractActionMessage = _contractActionMessage,
            FactoryActionMessage = _factoryActionMessage,
            ResearchActionMessage = _researchActionMessage,
        };

        for (var index = 0; index < _savedEngines.Count; index++)
        {
            var saved = _savedEngines[index];
            payload.SavedEngines.Add(new SavedEngineDesignState
            {
                Name = saved.Name,
                ResearchId = saved.ResearchId,
                EngineDefinitionId = saved.Definition.Id,
                Parameters = saved.Parameters.Clone(),
            });
        }

        for (var index = 0; index < _savedFamilies.Count; index++)
        {
            var saved = _savedFamilies[index];
            payload.SavedFamilies.Add(new SavedFamilyDesignState
            {
                Name = saved.Name,
                FamilyId = saved.FamilyId,
                LockedEngineCount = saved.LockedEngineCount,
                LockedStructureType = saved.LockedStructureType,
                LockedMaterialType = saved.LockedMaterialType,
                WingPosition = saved.WingPosition,
                AllowedEngineFamily = saved.AllowedEngineFamily,
            });
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var saved = _savedAircraftDesigns[index];
            payload.SavedAircraftDesigns.Add(new SavedAircraftDesignState
            {
                Name = saved.Name,
                EngineResearchId = saved.EngineResearchId,
                FamilyResearchId = saved.FamilyResearchId,
                TypeResearchId = saved.TypeResearchId,
                DesignId = saved.CatalogItem.DesignId,
                DisplayName = saved.CatalogItem.DisplayName,
                RequiredFactoryToolLevel = saved.CatalogItem.RequiredFactoryToolLevel,
                RequiredLineToolLevel = saved.CatalogItem.RequiredLineToolLevel,
                FamilyDefinition = saved.CatalogItem.Aircraft.FamilyDefinition,
                TypeDefinition = saved.CatalogItem.Aircraft.TypeDefinition,
            });
        }

        foreach (var entry in _researchModifiersByResearchId.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            payload.ResearchModifiersByResearchId.Add(new ResearchModifierStateEntry
            {
                ResearchId = entry.Key,
                ReliabilityPoints = entry.Value.ReliabilityPoints,
                ProductionOptimizationPoints = entry.Value.ProductionOptimizationPoints,
                FastDevelopmentPoints = entry.Value.FastDevelopmentPoints,
            });
        }

        foreach (var entry in _selectedWarehouseDesignByContractId)
        {
            payload.SelectedWarehouseDesignByContractId.Add(new StringStringStateEntry
            {
                Key = entry.Key,
                Value = entry.Value,
            });
        }

        foreach (var entry in _deliveredContractProfitById)
        {
            payload.DeliveredContractProfitById.Add(new StringDoubleStateEntry
            {
                Key = entry.Key,
                Value = entry.Value,
            });
        }

        if (_contractBoard is not null)
        {
            payload.ContractBoardSnapshot = _contractBoard.CreateSnapshot();
        }

        if (_researchBoard is not null)
        {
            payload.ResearchBoardSnapshot = _researchBoard.CreateSnapshot();
        }

        if (_productionFactory is not null)
        {
            payload.FactoryStateSnapshot = _productionFactory.CreateSnapshot();
        }

        if (_accountingSystem is not null)
        {
            payload.AccountingSnapshot = _accountingSystem.CreateSnapshot();
        }

        return payload;
    }

    private void loadGameState(RunnerGameSavePayload data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        _playerCompanyName = string.IsNullOrWhiteSpace(data.PlayerCompanyName)
            ? "Player Company"
            : data.PlayerCompanyName.Trim();

        var year = data.CurrentYear <= 0 ? SimulationDate.WrightTycoonStart.Year : data.CurrentYear;
        var month = data.CurrentMonth < 1 ? 1 : data.CurrentMonth > 12 ? 12 : data.CurrentMonth;
        _currentDate = new SimulationDate(year, month);
        _elapsedMonths = System.Math.Max(data.ElapsedMonths, 0);

        _tab = Mathf.Clamp(data.Tab, 0, _tabs.Length - 1);
        _financeSubTab = Mathf.Clamp(data.FinanceSubTab, 0, _financeSubTabs.Length - 1);
        _researchSubTab = Mathf.Clamp(data.ResearchSubTab, 0, _researchSubTabs.Length - 1);

        _selectedEngineDefinitionIndex = Mathf.Clamp(data.SelectedEngineDefinitionIndex, 0, System.Math.Max(_engineDefinitions.Count - 1, 0));
        _selectedAirframeDefinitionIndex = Mathf.Clamp(data.SelectedAirframeDefinitionIndex, 0, System.Math.Max(_airframeDefinitions.Count - 1, 0));

        _lockedEngineCount = NormalizeEngineCount(data.LockedEngineCount);
        _engineDesignerFamily = data.EngineDesignerFamily;
        _familyAllowedEngineFamily = data.FamilyAllowedEngineFamily == AircraftAllowedEngineFamily.Any
            ? AircraftAllowedEngineFamily.Propeller
            : data.FamilyAllowedEngineFamily;
        _selectedPistonArrangement = data.SelectedPistonArrangement;
        _selectedJetTurbineType = data.SelectedJetTurbineType;
        _lockedStructure = data.LockedStructure;
        _lockedMaterial = data.LockedMaterial;
        _selectedAircraftCategory = data.SelectedAircraftCategory;

        _engineDesignName = string.IsNullOrWhiteSpace(data.EngineDesignName) ? _engineDesignName : data.EngineDesignName;
        _aircraftDesignName = string.IsNullOrWhiteSpace(data.AircraftDesignName) ? _aircraftDesignName : data.AircraftDesignName;
        _familyName = string.IsNullOrWhiteSpace(data.FamilyName) ? _familyName : data.FamilyName;
        _typeName = string.IsNullOrWhiteSpace(data.TypeName) ? _typeName : data.TypeName;

        _requiredLineToolLevel = 1;
        _typeCrewCount = Mathf.Clamp(data.TypeCrewCount, PlaneDesignerBalance.Airframe.CrewMin, PlaneDesignerBalance.Airframe.CrewMax);
        _typeTailType = data.TypeTailType;
        _typeWingShape = data.TypeWingShape;
        var loadedWingProfile = Enum.IsDefined(typeof(WingProfileType), data.TypeWingProfile)
            ? data.TypeWingProfile
            : WingProfileType.Laminar6SeriesAerodynamic;
        _typeWingProfile = loadedWingProfile == WingProfileType.Standard
            ? WingProfileType.Laminar6SeriesAerodynamic
            : loadedWingProfile;
        _typeWingAreaSquareMeters = data.TypeWingAreaSquareMeters <= 0d ? _baseAirframeParameters.WingAreaSquareMeters : data.TypeWingAreaSquareMeters;
        _typeFlapType = data.TypeFlapType;
        _typeHasLeadingEdgeFlaps = data.TypeHasLeadingEdgeFlaps;
        _typeHasBackupSystems = data.TypeHasBackupSystems;
        _typeLandingGearType = data.TypeLandingGearType;
        _typeRunwayType = data.TypeRunwayType;
        _typeCowling = Mathf.Clamp((float)data.TypeCowling, 0.1f, 0.9f);
        _typeAerodynamicCleanlinessPercent = Mathf.Clamp((float)data.TypeAerodynamicCleanlinessPercent, (float)PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMinPercent, (float)PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent);
        _typeFuelLiters = Mathf.Clamp((float)data.TypeFuelLiters, (float)PlaneDesignerBalance.Airframe.FuelMinLiters, (float)PlaneDesignerBalance.Airframe.FuelMaxLiters);

        _researchModifiersByResearchId.Clear();
        if (data.ResearchModifiersByResearchId is not null)
        {
            for (var index = 0; index < data.ResearchModifiersByResearchId.Count; index++)
            {
                var state = data.ResearchModifiersByResearchId[index];
                if (state is null || string.IsNullOrWhiteSpace(state.ResearchId))
                {
                    continue;
                }

                _researchModifiersByResearchId[state.ResearchId] = new TypeResearchModifierPoints
                {
                    ReliabilityPoints = TypeResearchModifierPoints.ClampPoints(state.ReliabilityPoints),
                    ProductionOptimizationPoints = TypeResearchModifierPoints.ClampPoints(state.ProductionOptimizationPoints),
                    FastDevelopmentPoints = TypeResearchModifierPoints.ClampPoints(state.FastDevelopmentPoints),
                };
            }
        }

        _contractDeliverabilityFilter = data.ContractDeliverabilityFilter;
        _contractSpecificationFilter = data.ContractSpecificationFilter;
        _contractQuantityFilterMin = Mathf.Clamp(data.ContractQuantityFilterMin, 5, 500);
        _contractQuantityFilterMax = Mathf.Clamp(data.ContractQuantityFilterMax, 5, 500);
        _governmentContractNotebookState = ReadNotebookWindowSaveState(data.GovernmentContractNotebookState);
        ApplyGovernmentContractNotebookState();

        if (data.EngineParameters is not null)
        {
            _engineParameters = data.EngineParameters.Clone();
        }

        if (data.BaseAirframeParameters is not null)
        {
            _baseAirframeParameters = data.BaseAirframeParameters.Clone();
        }

        _savedEngines.Clear();
        if (data.SavedEngines is not null && _engineBuilder is not null)
        {
            for (var index = 0; index < data.SavedEngines.Count; index++)
            {
                var state = data.SavedEngines[index];
                if (state is null || string.IsNullOrWhiteSpace(state.EngineDefinitionId))
                {
                    continue;
                }

                var definition = _engineDefinitions.FirstOrDefault(def => string.Equals(def.Id, state.EngineDefinitionId, StringComparison.Ordinal));
                if (definition is null)
                {
                    continue;
                }

                try
                {
                    var parameters = state.Parameters?.Clone() ?? CreateDefaultEngineParameters(definition);
                    var built = _engineBuilder.Build(definition, parameters);
                    _savedEngines.Add(new SavedEngineDesign(
                        string.IsNullOrWhiteSpace(state.Name) ? "Loaded Engine" : state.Name,
                        string.IsNullOrWhiteSpace(state.ResearchId) ? CreateUniqueEngineResearchId(state.Name) : state.ResearchId,
                        built));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Save] Failed to restore engine '{state.Name}': {ex.Message}");
                }
            }
        }

        _savedFamilies.Clear();
        if (data.SavedFamilies is not null)
        {
            for (var index = 0; index < data.SavedFamilies.Count; index++)
            {
                var state = data.SavedFamilies[index];
                if (state is null)
                {
                    continue;
                }

                _savedFamilies.Add(new SavedFamilyDesign(
                    string.IsNullOrWhiteSpace(state.Name) ? "Loaded Family" : state.Name,
                    string.IsNullOrWhiteSpace(state.FamilyId) ? BuildFamilyId() : state.FamilyId,
                    NormalizeEngineCount(state.LockedEngineCount),
                    state.LockedStructureType,
                    state.LockedMaterialType,
                    state.WingPosition,
                    state.AllowedEngineFamily == AircraftAllowedEngineFamily.Any ? AircraftAllowedEngineFamily.Propeller : state.AllowedEngineFamily));
            }
        }

        _savedAircraftDesigns.Clear();
        if (data.SavedAircraftDesigns is not null && _aircraftFactory is not null)
        {
            for (var index = 0; index < data.SavedAircraftDesigns.Count; index++)
            {
                var state = data.SavedAircraftDesigns[index];
                if (state is null || state.FamilyDefinition is null || state.TypeDefinition is null)
                {
                    continue;
                }

                try
                {
                    var aircraft = _aircraftFactory.CreateAircraft(state.FamilyDefinition, state.TypeDefinition);
                    var designId = string.IsNullOrWhiteSpace(state.DesignId) ? state.TypeDefinition.Id : state.DesignId;
                    var displayName = string.IsNullOrWhiteSpace(state.DisplayName) ? state.Name : state.DisplayName;
                    var catalogItem = new FactoryAircraftTypeCatalogItem(
                        designId,
                        displayName,
                        aircraft,
                        System.Math.Max(state.RequiredFactoryToolLevel, 1),
                        System.Math.Max(state.RequiredLineToolLevel, 1));

                    _savedAircraftDesigns.Add(new SavedAircraftDesign(
                        string.IsNullOrWhiteSpace(state.Name) ? displayName : state.Name,
                        catalogItem,
                        string.IsNullOrWhiteSpace(state.EngineResearchId) ? "engine:unknown" : state.EngineResearchId,
                        string.IsNullOrWhiteSpace(state.FamilyResearchId) ? "family:unknown" : state.FamilyResearchId,
                        string.IsNullOrWhiteSpace(state.TypeResearchId) ? "type:unknown" : state.TypeResearchId));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Save] Failed to restore aircraft '{state.Name}': {ex.Message}");
                }
            }
        }

        _selectedWarehouseDesignByContractId.Clear();
        if (data.SelectedWarehouseDesignByContractId is not null)
        {
            for (var index = 0; index < data.SelectedWarehouseDesignByContractId.Count; index++)
            {
                var entry = data.SelectedWarehouseDesignByContractId[index];
                if (entry is null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
                {
                    continue;
                }

                _selectedWarehouseDesignByContractId[entry.Key] = entry.Value;
            }
        }

        _deliveredContractProfitById.Clear();
        if (data.DeliveredContractProfitById is not null)
        {
            for (var index = 0; index < data.DeliveredContractProfitById.Count; index++)
            {
                var entry = data.DeliveredContractProfitById[index];
                if (entry is null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                _deliveredContractProfitById[entry.Key] = entry.Value;
            }
        }

        if (data.ContractBoardSnapshot is not null && _contractBoard is not null)
        {
            _contractBoard.RestoreSnapshot(data.ContractBoardSnapshot);
        }

        if (data.ResearchBoardSnapshot is not null && _researchBoard is not null)
        {
            _researchBoard.RestoreSnapshot(data.ResearchBoardSnapshot);
        }

        if (data.FactoryStateSnapshot is not null && _productionFactory is not null)
        {
            _productionFactory.RestoreSnapshot(data.FactoryStateSnapshot);
        }

        if (data.AccountingSnapshot is not null && _accountingSystem is not null)
        {
            _accountingSystem.RestoreSnapshot(data.AccountingSnapshot);
        }

        EnsureResearchModifierEntriesForSavedDesigns();

        _selectedSavedEngineIndex = _savedEngines.Count == 0
            ? -1
            : Mathf.Clamp(data.SelectedSavedEngineIndex, 0, _savedEngines.Count - 1);
        _selectedSavedFamilyIndex = _savedFamilies.Count == 0
            ? -1
            : Mathf.Clamp(data.SelectedSavedFamilyIndex, 0, _savedFamilies.Count - 1);
        _selectedSavedAircraftDesignIndex = _savedAircraftDesigns.Count == 0
            ? -1
            : Mathf.Clamp(data.SelectedSavedAircraftDesignIndex, 0, _savedAircraftDesigns.Count - 1);

        _contractActionMessage = data.ContractActionMessage ?? string.Empty;
        _factoryActionMessage = data.FactoryActionMessage ?? string.Empty;
        _researchActionMessage = data.ResearchActionMessage ?? string.Empty;

        _lastGeneratedContracts.Clear();
        _lastFactoryReport = null;

        RebuildEnginePreview();
        RebuildAircraftPreview();

        if (_contractBoard is not null)
        {
            PruneContractDeliverySelections(_contractBoard.OpenContracts);
        }

        ApplyGovernmentContractNotebookState();
        RefreshGovernmentContractNotebookWindowContent();
    }

    private static JsonSerializerSettings CreateSaveJsonSettings()
    {
        return new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };
    }
    private void DrawEngineTab()
    {
        DrawSection("Engine Design (M2)");
        GUILayout.Label("Select piston/jet branch, then tune only parameters that belong to that branch.");

        _engineDesignName = DrawText("Design Name", _engineDesignName);
        _engineDesignerFamily = DrawEngineFamilySelector("Engine Type", _engineDesignerFamily);

        var requestedType = _engineDesignerFamily == EngineDesignerFamily.Piston
            ? DrawPistonArrangementSelector("Cylinder Arrangement", _selectedPistonArrangement)
            : DrawJetTurbineTypeSelector("Turbine Type", _selectedJetTurbineType);

        if (_engineDesignerFamily == EngineDesignerFamily.Piston)
        {
            _selectedPistonArrangement = requestedType;
        }
        else
        {
            _selectedJetTurbineType = requestedType;
        }

        if (!TryResolveEngineDefinitionForType(requestedType, out var selectedDefinitionIndex))
        {
            DrawError($"No engine definitions found for {requestedType}. Add one in engine-definitions.json.");
            return;
        }

        _selectedEngineDefinitionIndex = selectedDefinitionIndex;
        _engineDefinitionId = _engineDefinitions[_selectedEngineDefinitionIndex].Id;
        GUILayout.Label($"Definition (auto): {_engineDefinitions[_selectedEngineDefinitionIndex].DisplayName}");

        if (_engineDesignerFamily == EngineDesignerFamily.Piston)
        {
            var maxPistonCylinders = requestedType == EngineType.Inline
                ? EngineDesignerBalance.Engine.InlineCylinderMax
                : EngineDesignerBalance.Engine.PistonCylinderMax;
            _engineParameters.CylinderCount = Mathf.Clamp(
                _engineParameters.CylinderCount,
                EngineDesignerBalance.Engine.PistonCylinderMin,
                maxPistonCylinders);
            _engineParameters.CylinderCount = DrawIntSlider(
                "Number Of Cylinders",
                _engineParameters.CylinderCount,
                EngineDesignerBalance.Engine.PistonCylinderMin,
                maxPistonCylinders);
            _engineParameters.CompressionRatio = DrawSlider(
                "Compression Ratio",
                _engineParameters.CompressionRatio,
                EngineDesignerBalance.Engine.PistonCompressionMin,
                EngineDesignerBalance.Engine.PistonCompressionMax,
                "0.00");
            _engineParameters.OptimizationAltitudeMeters = DrawSlider(
                "Running Optimization Height (m)",
                _engineParameters.OptimizationAltitudeMeters,
                EngineDesignerBalance.Engine.PistonOptimizationMin,
                EngineDesignerBalance.Engine.PistonOptimizationMax,
                "0");
            _engineParameters.BoostType = DrawPistonBoostSelector("Boost", _engineParameters.BoostType);
            _engineParameters.PistonInjectionType = DrawPistonInjectionSelector("Injection Type", _engineParameters.PistonInjectionType);
            _engineParameters.MaximumSpeed = DrawSlider(
                "Maximum Speed (RPM)",
                _engineParameters.MaximumSpeed,
                EngineDesignerBalance.Engine.PistonSpeedMinRpm,
                EngineDesignerBalance.Engine.PistonSpeedMaxRpm,
                "0");
            _engineParameters.DisplacementLiters = DrawSlider("Displacement (L)", _engineParameters.DisplacementLiters, 1d, 80d, "0.0");
            _engineParameters.EfficiencyOptimizationsPercent = DrawSlider(
                "Efficiency Optimizations (%)",
                _engineParameters.EfficiencyOptimizationsPercent,
                EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent,
                EngineDesignerBalance.Engine.EfficiencyOptimizationMaxPercent,
                "0.0");
            _engineParameters.HasAfterburner = false;
        }
        else
        {
            var turbineDiameterCm = _engineParameters.TurbineDiameterMeters * 100d;
            turbineDiameterCm = DrawSlider(
                "Turbine Diameter (cm)",
                turbineDiameterCm,
                EngineDesignerBalance.Engine.JetTurbineDiameterMinMeters * 100d,
                EngineDesignerBalance.Engine.JetTurbineDiameterMaxMeters * 100d,
                "0");
            _engineParameters.TurbineDiameterMeters = turbineDiameterCm / 100d;
            if (requestedType == EngineType.Turbofan)
            {
                _engineParameters.BypassRatio = DrawSlider(
                    "Bypass Ratio",
                    _engineParameters.BypassRatio,
                    EngineDesignerBalance.Engine.JetBypassRatioMin,
                    EngineDesignerBalance.Engine.JetBypassRatioMax,
                    "0.00");
            }
            else
            {
                _engineParameters.BypassRatio = 0d;
                GUILayout.Label("Bypass Ratio: 0.00 (Fixed for Turbojet)");
            }

            _engineParameters.CompressionRatio = DrawSlider(
                "Compression Ratio",
                _engineParameters.CompressionRatio,
                EngineDesignerBalance.Engine.JetCompressionMin,
                EngineDesignerBalance.Engine.JetCompressionMax,
                "0.0");
            _engineParameters.MaximumSpeed = DrawSlider(
                "Maximum Speed (RPM)",
                _engineParameters.MaximumSpeed,
                EngineDesignerBalance.Engine.JetSpeedMinRpm,
                EngineDesignerBalance.Engine.JetSpeedMaxRpm,
                "0");
            _engineParameters.HasAfterburner = GUILayout.Toggle(_engineParameters.HasAfterburner, "Afterburner");
            _engineParameters.BoostType = BoostType.None;
            _engineParameters.EfficiencyOptimizationsPercent = EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent;
        }

        _engineParameters.CoolingEfficiency = DrawSlider("Cooling Efficiency", _engineParameters.CoolingEfficiency, 0d, 1d, "0.00");

        RebuildEnginePreview();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Engine", GUILayout.Height(30f)))
        {
            SaveCurrentEngine();
        }

        if (GUILayout.Button("Reset Parameters", GUILayout.Height(30f)))
        {
            _engineParameters = CreateDefaultEngineParameters(_engineDefinitions[_selectedEngineDefinitionIndex]);
            _enginePreviewError = string.Empty;
            RebuildEnginePreview();
        }

        GUILayout.EndHorizontal();

        DrawSection("Saved Engines");
        if (_savedEngines.Count == 0)
        {
            GUILayout.Label("None yet. Save one to use in the airframe tab.");
            return;
        }

        var toDelete = -1;
        for (var i = 0; i < _savedEngines.Count; i++)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            var selected = _selectedSavedEngineIndex == i;
            if (GUILayout.Toggle(selected, string.Empty, GUILayout.Width(22f)))
            {
                _selectedSavedEngineIndex = i;
            }

            GUILayout.Label(BuildEngineDisplayName(_savedEngines[i].Name, _savedEngines[i].Definition.EngineType, _savedEngines[i].Parameters));
            if (GUILayout.Button("Load", GUILayout.Width(60f)))
            {
                LoadSavedEngine(i);
            }

            if (GUILayout.Button("Delete", GUILayout.Width(60f)))
            {
                toDelete = i;
            }

            GUILayout.EndHorizontal();
        }

        if (toDelete >= 0 && toDelete < _savedEngines.Count)
        {
            _savedEngines.RemoveAt(toDelete);
            if (_savedEngines.Count == 0)
            {
                _selectedSavedEngineIndex = -1;
            }
            else if (_selectedSavedEngineIndex >= _savedEngines.Count)
            {
                _selectedSavedEngineIndex = _savedEngines.Count - 1;
            }
        }
    }

    private void DrawEngineReport()
    {
        DrawSection("Engine Performance");
        if (!string.IsNullOrWhiteSpace(_enginePreviewError))
        {
            DrawError(_enginePreviewError);
            return;
        }

        if (_enginePreview is null)
        {
            GUILayout.Label("No preview available.");
            return;
        }

        var report = _enginePreview.Performance;
        var outputLabel = report.OutputKind == EngineOutputKind.PowerKilowatts ? "Power (kW)" : "Thrust (N)";
        var isJet = IsJetFamily(_enginePreview.Definition.EngineType);
        var enginePreviewKind = GetEnginePreviewKind(_enginePreview.Definition.EngineType, _enginePreview.Parameters.BypassRatio);

        DrawSection("Engine Visual");
        var engineVisualRect = GUILayoutUtility.GetRect(110f, 230f, GUILayout.ExpandWidth(true));
        EnginePreviewPainter.Draw(engineVisualRect, enginePreviewKind);
        GUILayout.Label($"Visual Type: {EnginePreviewPainter.GetLabel(enginePreviewKind)}");

        GUILayout.Label($"Definition: {_enginePreview.Definition.DisplayName} ({_enginePreview.Definition.EngineType})");
        if (isJet)
        {
            DrawMetric("Turbine Diameter (cm)", _enginePreview.Parameters.TurbineDiameterMeters * 100d, "0");
            if (_enginePreview.Definition.EngineType == EngineType.Turbofan)
            {
                DrawMetric("Bypass Ratio", _enginePreview.Parameters.BypassRatio, "0.00");
            }
            else
            {
                GUILayout.Label("Bypass Ratio: 0.00 (Turbojet fixed)");
            }
            DrawMetric("Compression Ratio", _enginePreview.Parameters.CompressionRatio, "0.0");
            DrawMetric("Maximum Speed (RPM)", _enginePreview.Parameters.MaximumSpeed, "0");
            GUILayout.Label($"Afterburner: {(_enginePreview.Parameters.HasAfterburner ? "Enabled" : "Disabled")}");
        }
        else
        {
            DrawMetric("Cylinder Count", _enginePreview.Parameters.CylinderCount, "0");
            DrawMetric("Compression Ratio", _enginePreview.Parameters.CompressionRatio, "0.00");
            DrawMetric("Optimization Height (m)", _enginePreview.Parameters.OptimizationAltitudeMeters, "0");
            DrawMetric("Efficiency Optimizations (%)", _enginePreview.Parameters.EfficiencyOptimizationsPercent, "0.0");
            GUILayout.Label($"Boost: {_enginePreview.Parameters.BoostType}");
            GUILayout.Label($"Injection: {_enginePreview.Parameters.PistonInjectionType}");
            DrawMetric("Maximum Speed (RPM)", _enginePreview.Parameters.MaximumSpeed, "0");
        }

        var fuelMetricLabel = isJet ? "L/N" : "L/kWh";
        foreach (var p in report.AltitudePerformance)
        {
            var fuelPerOutput = CalculateFuelPerOutputMetric(
                p.Output,
                p.FuelConsumptionKilogramsPerHour,
                isJet);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"{FormatUiNumber(p.AltitudeMeters, "0")}m", GUILayout.Width(70f));
            GUILayout.Label($"{outputLabel}: {FormatUiNumber(p.Output, "0.0")}", GUILayout.Width(170f));
            GUILayout.Label($"Fuel: {FormatUiNumber(p.FuelConsumptionKilogramsPerHour, "0.0")} kg/h", GUILayout.Width(160f));
            GUILayout.Label($"{fuelMetricLabel}: {FormatUiNumber(fuelPerOutput, "0.0000")}");
            GUILayout.EndHorizontal();
        }

        DrawMetric("Reliability", report.Reliability, "0.000");
        DrawMetric("Weight (kg)", report.Weight.Kilograms, "0.0");
        DrawMetric("Engine Length (m)", report.EngineLengthMeters, "0.00");
        DrawMetric("Engine Frontal Area (m2)", report.EngineFrontalAreaSquareMeters, "0.000");
        DrawMetric("Cost", report.Cost, "0");
        DrawMetric("Production MD", report.ProductionManDays, "0.0");

        DrawSection("Effect Summary");
        GUILayout.Label("Higher compression/boost gives more output but higher fuel burn and lower reliability.");
        GUILayout.Label("Turbine diameter and bypass ratio strongly affect jet-family engines.");
        GUILayout.Label("Cooling improves durability and operating margin.");
    }

    private void DrawAirframeTab()
    {
        DrawSection("Airframe + Family/Type (M3 + M4)");

        if (_savedEngines.Count == 0)
        {
            DrawError("No saved engine. Save an engine in the first tab.");
            if (GUILayout.Button("Go To Engine Tab", GUILayout.Height(30f)))
            {
                _tab = 0;
            }

            return;
        }

        DrawSection("Family");
        _familyName = DrawText("Family Name", _familyName);
        GUILayout.Label($"Family ID (auto): {BuildFamilyId()}");
        _lockedEngineCount = DrawIntSlider("Number Of Engines", _lockedEngineCount, 1, 6);
        _lockedStructure = DrawEnum("Structure Type", _lockedStructure);
        _lockedMaterial = DrawMaterialSelector("Material", _lockedMaterial);
        _baseAirframeParameters.WingPosition = DrawWingPositionSelector("Wing Position", _baseAirframeParameters.WingPosition);
        _familyAllowedEngineFamily = DrawFamilyAllowedEngineSelector("Engine Type", _familyAllowedEngineFamily);

        _baseAirframeParameters.EngineCount = _lockedEngineCount;
        _baseAirframeParameters.StructureType = _lockedStructure;
        _baseAirframeParameters.MaterialType = _lockedMaterial;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Family", GUILayout.Height(28f)))
        {
            SaveCurrentFamily();
        }

        if (GUILayout.Button("Reset Family", GUILayout.Height(28f)))
        {
            ResetFamilyConfig();
        }

        GUILayout.EndHorizontal();

        DrawSection("Saved Families");
        if (_savedFamilies.Count == 0)
        {
            GUILayout.Label("None yet. Save one to reuse family setup separately.");
        }
        else
        {
            var labels = _savedFamilies
                .Select(saved =>
                    BuildFamilyDisplayName(
                        saved.Name,
                        saved.LockedEngineCount,
                        saved.LockedMaterialType,
                        saved.LockedStructureType,
                        saved.AllowedEngineFamily))
                .ToArray();
            _selectedSavedFamilyIndex = DrawListSelector("Select Saved Family", _selectedSavedFamilyIndex < 0 ? 0 : _selectedSavedFamilyIndex, labels);

            if (_selectedSavedFamilyIndex >= 0 && _selectedSavedFamilyIndex < _savedFamilies.Count)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Load Family", GUILayout.Height(26f)))
                {
                    LoadSavedFamily(_selectedSavedFamilyIndex);
                }

                if (GUILayout.Button("Delete Family", GUILayout.Height(26f)))
                {
                    _savedFamilies.RemoveAt(_selectedSavedFamilyIndex);
                    if (_savedFamilies.Count == 0)
                    {
                        _selectedSavedFamilyIndex = -1;
                    }
                    else if (_selectedSavedFamilyIndex >= _savedFamilies.Count)
                    {
                        _selectedSavedFamilyIndex = _savedFamilies.Count - 1;
                    }
                }

                GUILayout.EndHorizontal();
            }
        }

        DrawSection("Type");
        _typeName = BuildTypeDisplayName();
        GUILayout.Label($"Type Name (auto): {_typeName}");
        GUILayout.Label($"Type ID (auto): {BuildTypeId()}");
        _selectedAircraftCategory = DrawAircraftCategorySelector("Aircraft Category", _selectedAircraftCategory);
        _typeCrewCount = DrawIntSlider(
            "Crew Members",
            _typeCrewCount,
            PlaneDesignerBalance.Airframe.CrewMin,
            PlaneDesignerBalance.Airframe.CrewMax);
        _typeTailType = DrawTailSelector("Tail Type", _typeTailType);
        _typeWingShape = DrawEnum("Wing Shape", _typeWingShape);
        _typeWingProfile = DrawWingProfileSelector("Wing Profile", _typeWingProfile);
        GUILayout.Label(_typeWingProfile switch
        {
            WingProfileType.Laminar6SeriesAerodynamic =>
                "Laminar 6-series: up to +5% lift and -5% drag (full effect at 50 m2, fades to 0 at 500 m2).",
            WingProfileType.Naca4DigitLight =>
                "NACA 4-digit Light: -10% airframe cost, -10% weight, and drag rises by up to +80% from 250 to 1,000 km/h.",
            WingProfileType.SymmetricalAcrobatic =>
                "Symmetrical Acrobatic: +10% maneuverability, +20% airframe cost, -10% lift.",
            _ =>
                "Laminar 6-series: up to +5% lift and -5% drag (full effect at 50 m2, fades to 0 at 500 m2).",
        });
        _typeWingAreaSquareMeters = DrawSlider("Wing Area (m2)", _typeWingAreaSquareMeters, 1d, 1000d, "0.0");

        DrawSection("Type Engine");
        var compatibleEngineIndices = GetCompatibleSavedEngineIndices();
        if (compatibleEngineIndices.Count == 0)
        {
            DrawError($"No saved {GetAllowedEngineFamilyLabel(_familyAllowedEngineFamily).ToLowerInvariant()} engines available for this family.");
        }
        else
        {
            var compatibleLabels = compatibleEngineIndices
                .Select(engineIndex =>
                {
                    var savedEngine = _savedEngines[engineIndex];
                    return BuildEngineDisplayName(savedEngine.Name, savedEngine.Definition.EngineType, savedEngine.Parameters);
                })
                .ToArray();
            var selectedCompatibleIndex = ResolveSelectedCompatibleEngineOptionIndex(compatibleEngineIndices);
            selectedCompatibleIndex = DrawListSelector("Assigned Engine", selectedCompatibleIndex, compatibleLabels);
            _selectedSavedEngineIndex = compatibleEngineIndices[selectedCompatibleIndex];
        }

        _typeFlapType = DrawTrailingFlapSelector("Flaps", _typeFlapType);
        _typeHasLeadingEdgeFlaps = GUILayout.Toggle(_typeHasLeadingEdgeFlaps, "Leading Edge Flaps");
        _typeHasBackupSystems = GUILayout.Toggle(_typeHasBackupSystems, "Backup Systems (+0.2 reliability, +10% weight, +3% drag)");
        _typeLandingGearType = DrawLandingGearSelector("Gear Type", _typeLandingGearType);
        _typeRunwayType = DrawRunwayTypeSelector("Runway Type", _typeRunwayType);
        _typeCowling = DrawSlider("Cowling", _typeCowling, 0.1d, 0.9d, "0.00");
        _typeAerodynamicCleanlinessPercent = DrawSlider(
            "Aerodynamic Cleanliness (%)",
            _typeAerodynamicCleanlinessPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMinPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent,
            "0.0");
        _typeFuelLiters = DrawSlider(
            "Fuel (L)",
            _typeFuelLiters,
            PlaneDesignerBalance.Airframe.FuelMinLiters,
            PlaneDesignerBalance.Airframe.FuelMaxLiters,
            "0");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Type", GUILayout.Height(28f)))
        {
            SyncTypeAirframeFromBase();
        }

        GUILayout.EndHorizontal();

        _aircraftDesignName = DrawText("Saved Aircraft Name", _aircraftDesignName);

        if (GUILayout.Button("Save Aircraft Design", GUILayout.Height(30f)))
        {
            SaveCurrentAircraftDesign();
        }

        DrawSection("Saved Aircraft Designs");
        if (_savedAircraftDesigns.Count == 0)
        {
            GUILayout.Label("None yet. Save one to use in Factory production lines.");
        }
        else
        {
            var labels = _savedAircraftDesigns
                .Select(design =>
                    $"{design.Name} | {AircraftCategoryCatalog.GetProfile(design.CatalogItem.Category).DisplayName} | {design.CatalogItem.Aircraft.Engine.Definition.DisplayName} x{FormatUiInteger(design.CatalogItem.Aircraft.Airframe.Parameters.EngineCount)} | {GetAircraftDesignResearchStatus(design)}")
                .ToArray();
            _selectedSavedAircraftDesignIndex = DrawListSelector("Select Saved Design", _selectedSavedAircraftDesignIndex < 0 ? 0 : _selectedSavedAircraftDesignIndex, labels);

            if (_selectedSavedAircraftDesignIndex >= 0 && _selectedSavedAircraftDesignIndex < _savedAircraftDesigns.Count)
            {
                if (GUILayout.Button("Delete Selected Aircraft Design", GUILayout.Height(26f)))
                {
                    var designToDelete = _savedAircraftDesigns[_selectedSavedAircraftDesignIndex];
                    var warehouseUnits = _productionFactory?.GetWarehouseUnits(designToDelete.CatalogItem.DesignId) ?? 0;
                    if (warehouseUnits > 0)
                    {
                        _factoryActionMessage = $"Cannot delete '{designToDelete.Name}'. Warehouse still has {FormatUiInteger(warehouseUnits)} units.";
                    }
                    else
                    {
                        _savedAircraftDesigns.RemoveAt(_selectedSavedAircraftDesignIndex);
                        var contractsToClear = _selectedWarehouseDesignByContractId
                            .Where(entry => string.Equals(entry.Value, designToDelete.CatalogItem.DesignId, StringComparison.Ordinal))
                            .Select(entry => entry.Key)
                            .ToArray();
                        for (var contractIndex = 0; contractIndex < contractsToClear.Length; contractIndex++)
                        {
                            _selectedWarehouseDesignByContractId.Remove(contractsToClear[contractIndex]);
                        }

                        if (_savedAircraftDesigns.Count == 0)
                        {
                            _selectedSavedAircraftDesignIndex = -1;
                        }
                        else if (_selectedSavedAircraftDesignIndex >= _savedAircraftDesigns.Count)
                        {
                            _selectedSavedAircraftDesignIndex = _savedAircraftDesigns.Count - 1;
                        }
                    }
                }
            }
        }

        RebuildAircraftPreview();
        if (_aircraftPreview is not null)
        {
            DrawSection("Live Category Control");
            var liveCategoryProfile = AircraftCategoryCatalog.GetProfile(_aircraftPreview.FamilyDefinition.Category);
            var liveCompliance = _categoryComplianceEvaluator.Evaluate(_aircraftPreview, liveCategoryProfile);
            if (liveCompliance.IsCompliant)
            {
                GUILayout.Label("Status: OK. Current aircraft fits selected category.");
            }
            else
            {
                DrawError("Status: WARNING. Current aircraft does not fit selected category:");
                for (var warningIndex = 0; warningIndex < liveCompliance.UnmetRequirements.Count; warningIndex++)
                {
                    DrawError($"- {liveCompliance.UnmetRequirements[warningIndex]}");
                }
            }
        }

        if (GUILayout.Button("Reset Family + Type Config", GUILayout.Height(30f)))
        {
            ResetFamilyConfig();
            SyncTypeAirframeFromBase();
        }
    }

    private void DrawAirframeReport()
    {
        DrawSection("Aircraft Performance");
        RebuildAircraftPreview();

        if (!string.IsNullOrWhiteSpace(_aircraftPreviewError))
        {
            DrawError(_aircraftPreviewError);
            return;
        }

        if (_aircraftPreview is null)
        {
            GUILayout.Label("No preview available.");
            return;
        }

        var perf = _aircraftPreview.Performance;
        var airframePerf = _aircraftPreview.Airframe.Performance;
        var categoryProfile = AircraftCategoryCatalog.GetProfile(_aircraftPreview.FamilyDefinition.Category);

        GUILayout.Label($"Family: {_aircraftPreview.FamilyDefinition.DisplayName}");
        GUILayout.Label($"Category: {categoryProfile.DisplayName}");
        GUILayout.Label($"Type: {_aircraftPreview.TypeDefinition.DisplayName}");
        GUILayout.Label($"Engine: {_aircraftPreview.Engine.Definition.DisplayName} x{FormatUiInteger(_aircraftPreview.Airframe.Parameters.EngineCount)}");
        GUILayout.Label($"Wing/Tail/Profile: {_aircraftPreview.Airframe.Parameters.WingPosition} {_aircraftPreview.Airframe.Parameters.WingShape}, {_aircraftPreview.Airframe.Parameters.TailType}, {_aircraftPreview.Airframe.Parameters.WingProfile}");
        GUILayout.Label($"Flaps: {_aircraftPreview.Airframe.Parameters.FlapType}, Leading Edge: {_aircraftPreview.Airframe.Parameters.HasLeadingEdgeFlaps}");
        GUILayout.Label($"Backup Systems: {_aircraftPreview.Airframe.Parameters.HasBackupSystems}");
        GUILayout.Label("Research modifiers are configured per proposal in the Research tab.");
        GUILayout.Label(
            $"Runway: {_aircraftPreview.Airframe.Parameters.RunwayType} ({FormatUiNumber(GetRunwayLengthMeters(_aircraftPreview.Airframe.Parameters.RunwayType), "0")} m)");

        DrawSection("Aircraft Visual");
        var aircraftVisualRect = GUILayoutUtility.GetRect(140f, 290f, GUILayout.ExpandWidth(true));
        var rendered3d = _aircraftPreview3DRenderer.Draw(
            aircraftVisualRect,
            _aircraftPreview.Airframe.Parameters,
            _aircraftPreview.Engine.Definition.EngineType,
            _aircraftPreview.Engine.Parameters.BypassRatio);
        if (!rendered3d)
        {
            // Fallback if 3D preview cannot be rendered in current graphics mode.
            CartoonAircraftPainter.Draw(
                aircraftVisualRect,
                _aircraftPreview.Airframe.Parameters,
                _aircraftPreview.Engine.Definition.EngineType,
                _aircraftPreview.Engine.Parameters.BypassRatio);
        }

        GUILayout.Label("3D visual reacts to wing shape/position, tail, landing gear, engine count, material and engine type.");

        foreach (var p in perf.MaxSpeedByAltitude)
        {
            DrawMetric($"Max Speed @{FormatUiNumber(p.AltitudeMeters, "0")}m (km/h)", p.MaxSpeedMetersPerSecond * 3.6d, "0.0");
        }

        DrawMetric("Range (km)", perf.RangeKilometers, "0.0");
        DrawMetric("Optimal Cruise Speed (km/h)", perf.OptimalCruiseSpeedMetersPerSecond * 3.6d, "0.0");
        DrawMetric("Optimal Cruise Altitude (m)", perf.OptimalCruiseAltitudeMeters, "0");
        DrawMetric("Cruise Fuel Burn Avg (kg/h)", perf.AverageCruiseFuelBurnKilogramsPerHour, "0.0");
        DrawMetric("Cruise Fuel Burn Full Tank (kg/h)", perf.FullFuelCruiseBurnKilogramsPerHour, "0.0");
        DrawMetric("Cruise Fuel Burn Empty Tank (kg/h)", perf.EmptyFuelCruiseBurnKilogramsPerHour, "0.0");
        DrawMetric("Useful Weight (kg)", perf.PayloadCapacity.Kilograms, "0.0");
        DrawMetric("Maximum Takeoff Weight (kg)", perf.MaximumTakeoffWeightKilograms, "0.0");
        DrawMetric("Aircraft Weight With Fuel (kg)", perf.AircraftWeightWithFuelKilograms, "0.0");
        DrawMetric("Empty Aircraft Weight (kg)", perf.EmptyAircraftWeightKilograms, "0.0");
        if (categoryProfile.HasPassengerCapacityRequirement)
        {
            DrawMetric($"Passengers (Payload/{FormatUiNumber(PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger, "0")}kg)", perf.PassengerCapacity, "0");
        }

        DrawMetric("Total Weight (kg)", perf.TotalWeight.Kilograms, "0.0");
        DrawMetric("Overall Reliability", perf.OverallReliability, "0.000");

        DrawMetric("Drag Coefficient (Cd)", airframePerf.DragCoefficient, "0.000");
        DrawMetric("Lift Coefficient (Cl)", airframePerf.LiftCoefficient, "0.000");
        DrawMetric("Aerodynamic Cleanliness (%)", airframePerf.AerodynamicCleanlinessPercent, "0.0");
        DrawMetric("Aircraft Length (m)", perf.AircraftLengthMeters, "0.00");
        DrawMetric("Aircraft Frontal Area (m2)", perf.AircraftFrontalAreaSquareMeters, "0.00");
        DrawMetric("Total Drag Area (Cd*A, m2)", perf.TotalDragAreaSquareMeters, "0.000");
        DrawMetric("Maneuverability", airframePerf.Maneuverability, "0.0");

        DrawMetric("Production Cost", perf.TotalProductionCost, "0");
        DrawMetric("Production MD", perf.TotalProductionManDays, "0.0");
        DrawMetric("Development Cost", _aircraftPreview.Development.DevelopmentCost, "0");
        DrawMetric("Development MD", _aircraftPreview.Development.DevelopmentManDays, "0.0");
        if (_aircraftPreview.Airframe.Parameters.MaterialType == MaterialType.WoodFabric
            && perf.TotalWeight.Kilograms > PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms)
        {
            DrawError(
                $"WARNING: Wood/Fabric aircraft exceeded weight limit ({FormatUiNumber(perf.TotalWeight.Kilograms, "0.0")} kg > " +
                $"{FormatUiNumber(PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms, "0.0")} kg)");
        }

        DrawSection("Category Control (M3)");
        var compliance = _categoryComplianceEvaluator.Evaluate(_aircraftPreview, categoryProfile);
        GUILayout.Label($"Target Category: {categoryProfile.DisplayName}");
        if (categoryProfile.HasPassengerCapacityRequirement)
        {
            GUILayout.Label(
                $"Passenger target: {FormatUiInteger(categoryProfile.GetMinimumPassengerCapacity())} - {FormatUiInteger(categoryProfile.GetMaximumPassengerCapacity())} " +
                $"(from payload / {FormatUiNumber(PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger, "0")} kg)");
        }

        if (compliance.IsCompliant)
        {
            GUILayout.Label("Status: OK. Aircraft meets category requirements.");
        }
        else
        {
            DrawError("Status: WARNING. Aircraft does not meet category requirements:");
            for (var index = 0; index < compliance.UnmetRequirements.Count; index++)
            {
                DrawError($"- {compliance.UnmetRequirements[index]}");
            }
        }


    }

    private void DrawContractsTab()
    {
        DrawSection("Contracts (M6)");
        if (_contractBoard is null || _productionFactory is null || _contractWarehouseDeliveryService is null)
        {
            DrawError("Contract or factory system is not initialized.");
            return;
        }

        GUILayout.Label("Contracts are generated automatically whenever Next Month is pressed.");
        GUILayout.Label("Delivery now pulls completed aircraft from warehouse stock.");
        GUILayout.Label($"Current Month Index: {FormatUiInteger(_contractBoard.CurrentMonthIndex)}");
        GUILayout.Label($"Open: {FormatUiInteger(_contractBoard.OpenContracts.Count)} | Delivered: {FormatUiInteger(_contractBoard.DeliveredContracts.Count)} | Canceled: {FormatUiInteger(_contractBoard.CanceledContracts.Count)}");
        GUILayout.Label($"Warehouse Units: {FormatUiInteger(_productionFactory.WarehouseUnitsByDesign.Values.Sum())}");

        if (!string.IsNullOrWhiteSpace(_contractActionMessage))
        {
            GUILayout.Label(_contractActionMessage);
        }

        DrawSection("Filters");
        _contractDeliverabilityFilter = DrawContractDeliverabilityFilter("1) Delivery Filter", _contractDeliverabilityFilter);
        _contractSpecificationFilter = DrawContractSpecificationFilter("2) Matching Plane Filter", _contractSpecificationFilter);
        _contractQuantityFilterMin = DrawIntSlider("3) Required Aircraft Min", _contractQuantityFilterMin, 5, 500);
        _contractQuantityFilterMax = DrawIntSlider("3) Required Aircraft Max", _contractQuantityFilterMax, 5, 500);
        if (_contractQuantityFilterMin > _contractQuantityFilterMax)
        {
            _contractQuantityFilterMax = _contractQuantityFilterMin;
        }

        if (GUILayout.Button("Reset Contract Filters", GUILayout.Height(26f)))
        {
            _contractDeliverabilityFilter = ContractDeliverabilityFilter.All;
            _contractSpecificationFilter = ContractSpecificationFilter.All;
            _contractQuantityFilterMin = 5;
            _contractQuantityFilterMax = 500;
        }

        if (GUILayout.Button("Go To Airframe + Type Tab", GUILayout.Height(30f)))
        {
            _tab = 1;
        }

        if (_lastGeneratedContracts.Count == 0)
        {
            return;
        }

        DrawSection("Last Generated Batch");
        for (var index = 0; index < _lastGeneratedContracts.Count; index++)
        {
            var contract = _lastGeneratedContracts[index];
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(contract.Id, GUILayout.Width(150f));
            GUILayout.Label(contract.CompanyName, GUILayout.Width(180f));
            var categoryLabel = contract.Requirements.RequiredCategory.HasValue
                ? AircraftCategoryCatalog.GetProfile(contract.Requirements.RequiredCategory.Value).DisplayName
                : "Any Category";
            var contractTotalPrice = GetContractTotalPrice(contract);
            GUILayout.Label($"{categoryLabel} | {FormatUiInteger(contract.QuantityUnits)} units / {FormatUiInteger(contract.DeadlineMonths)} months | Price {FormatCurrency(contractTotalPrice)}");
            GUILayout.EndHorizontal();
        }
    }

    private void DrawContractsReport()
    {
        DrawSection("Contract Board");
        if (_contractBoard is null || _productionFactory is null || _contractWarehouseDeliveryService is null)
        {
            DrawError("Contract or factory system is not initialized.");
            return;
        }

        PruneContractDeliverySelections(_contractBoard.OpenContracts);
        var catalog = BuildProductionCatalog();
        var warehouseSnapshot = _productionFactory.WarehouseUnitsByDesign;
        var totalWarehouseUnits = warehouseSnapshot.Values.Sum();

        DrawSection("Warehouse Stock");
        DrawMetric("Total Stored Aircraft", totalWarehouseUnits, "0");
        if (warehouseSnapshot.Count == 0)
        {
            GUILayout.Label("Warehouse is empty. Produce aircraft in Factory tab first.");
        }
        else
        {
            foreach (var warehouseEntry in warehouseSnapshot.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var displayName = ResolveAircraftDesignName(warehouseEntry.Key);
                var categoryName = ResolveAircraftCategoryName(warehouseEntry.Key);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{displayName} ({categoryName})", GUILayout.Width(360f));
                GUILayout.Label($"{FormatUiInteger(warehouseEntry.Value)} units");
                GUILayout.EndHorizontal();
            }
        }

        var openContracts = _contractBoard.OpenContracts;
        var availabilitySnapshots = new List<ContractAvailabilitySnapshot>(openContracts.Count);
        for (var contractIndex = 0; contractIndex < openContracts.Count; contractIndex++)
        {
            availabilitySnapshots.Add(BuildContractAvailabilitySnapshot(openContracts[contractIndex], _contractBoard, catalog, warehouseSnapshot));
        }

        var filteredSnapshots = availabilitySnapshots
            .Where(MatchesContractFilters)
            .ToList();

        DrawMetric("Contracts Matching Filters", filteredSnapshots.Count, "0");
        if (openContracts.Count > 0 && filteredSnapshots.Count != openContracts.Count)
        {
            GUILayout.Label($"Filtered out: {FormatUiInteger(openContracts.Count - filteredSnapshots.Count)}");
        }

        if (openContracts.Count == 0)
        {
            GUILayout.Label("No open contracts. Generate new contracts from the Contracts tab.");
        }
        else if (filteredSnapshots.Count == 0)
        {
            GUILayout.Label("No open contracts match current filters.");
        }
        else
        {
            Contract? contractToDeliver = null;
            string? designIdToDeliver = null;
            double contractIncomeToBook = 0d;
            double contractProfitLossToBook = 0d;

            for (var index = 0; index < filteredSnapshots.Count; index++)
            {
                var snapshot = filteredSnapshots[index];
                var contract = snapshot.Contract;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{contract.Id} | {contract.CompanyName}");
                GUILayout.Label($"Quantity: {FormatUiInteger(contract.QuantityUnits)} units");
                GUILayout.Label($"Deadline: {FormatUiInteger(contract.RemainingMonths)} months remaining (initial {FormatUiInteger(contract.DeadlineMonths)})");
                GUILayout.Label($"Can deliver now: {(snapshot.CanBeDeliveredNow ? "Yes" : "No")} | Matching plane by specs: {(snapshot.HasMatchingPlaneBySpecification ? "Yes" : "No")}");
                DrawContractRequirements(contract.Requirements);

                var availableDesignIds = warehouseSnapshot
                    .Where(entry => entry.Value > 0 && catalog.ContainsKey(entry.Key))
                    .Select(entry => entry.Key)
                    .OrderBy(id => catalog[id].DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => id, StringComparer.Ordinal)
                    .ToArray();

                if (availableDesignIds.Length == 0)
                {
                    DrawError("Status: blocked");
                    GUILayout.Label("- No usable aircraft in warehouse.");
                    if (warehouseSnapshot.Count > 0 && catalog.Count == 0)
                    {
                        GUILayout.Label("- Save at least one aircraft design in Airframe + Type tab.");
                    }
                }
                else
                {
                    if (!_selectedWarehouseDesignByContractId.TryGetValue(contract.Id, out var selectedDesignId)
                        || Array.IndexOf(availableDesignIds, selectedDesignId) < 0)
                    {
                        selectedDesignId = availableDesignIds[0];
                        _selectedWarehouseDesignByContractId[contract.Id] = selectedDesignId;
                    }

                    var selectedDesignIndex = Array.IndexOf(availableDesignIds, selectedDesignId);
                    var designOptions = new string[availableDesignIds.Length];
                    for (var designIndex = 0; designIndex < availableDesignIds.Length; designIndex++)
                    {
                        var designId = availableDesignIds[designIndex];
                        var design = catalog[designId];
                        var stock = warehouseSnapshot[designId];
                        designOptions[designIndex] = $"{design.DisplayName} | stock {FormatUiInteger(stock)}";
                    }

                    selectedDesignIndex = DrawListSelector($"Delivery Source ({contract.Id})", selectedDesignIndex, designOptions);
                    selectedDesignIndex = Mathf.Clamp(selectedDesignIndex, 0, availableDesignIds.Length - 1);
                    selectedDesignId = availableDesignIds[selectedDesignIndex];
                    _selectedWarehouseDesignByContractId[contract.Id] = selectedDesignId;

                    var selectedAircraftDesign = catalog[selectedDesignId];
                    var selectedStock = warehouseSnapshot[selectedDesignId];
                    var hasRequiredStock = selectedStock >= contract.QuantityUnits;
                    var evaluation = _contractBoard.Evaluate(contract, selectedAircraftDesign.Aircraft);

                    DrawMetric("Stock For Selected Source", selectedStock, "0");
                    GUILayout.Label($"Source Category: {AircraftCategoryCatalog.GetProfile(selectedAircraftDesign.Category).DisplayName}");
                    DrawMetric("Source Reliability", selectedAircraftDesign.Aircraft.Performance.OverallReliability, "0.000");
                    DrawMetric("Source Weight (kg)", selectedAircraftDesign.Aircraft.Performance.TotalWeight.Kilograms, "0.0");
                    DrawMetric("Source Range (km)", selectedAircraftDesign.Aircraft.Performance.RangeKilometers, "0.0");
                    DrawMetric("Source Payload (kg)", selectedAircraftDesign.Aircraft.Performance.PayloadCapacity.Kilograms, "0.0");

                    var contractUnitPrice = GetContractUnitPrice(contract);
                    var contractTotalPrice = GetContractTotalPrice(contract);
                    var expectedProfitLoss = CalculateContractProfitLoss(contract, selectedAircraftDesign);
                    DrawMetric("Contract Unit Price", contractUnitPrice, "0.00");
                    DrawMetric("Contract Total Price", contractTotalPrice, "0.00");
                    DrawSignedCurrencyMetric("Expected Profit/Loss", expectedProfitLoss);

                    if (evaluation.CanDeliver && hasRequiredStock)
                    {
                        GUILayout.Label("Status: deliverable");
                        if (GUILayout.Button($"Deliver {FormatUiInteger(contract.QuantityUnits)} Units", GUILayout.Height(26f)))
                        {
                            contractToDeliver = contract;
                            designIdToDeliver = selectedDesignId;
                            contractIncomeToBook = contractTotalPrice;
                            contractProfitLossToBook = expectedProfitLoss;
                        }
                    }
                    else
                    {
                        DrawError("Status: blocked");
                        if (!hasRequiredStock)
                        {
                            GUILayout.Label($"- Warehouse has {FormatUiInteger(selectedStock)}, needs {FormatUiInteger(contract.QuantityUnits)}.");
                        }

                        for (var unmetIndex = 0; unmetIndex < evaluation.UnmetRequirements.Count; unmetIndex++)
                        {
                            GUILayout.Label($"- {evaluation.UnmetRequirements[unmetIndex]}");
                        }
                    }
                }

                GUILayout.EndVertical();
            }

            if (contractToDeliver is not null
                && !string.IsNullOrWhiteSpace(designIdToDeliver))
            {
                var result = _contractWarehouseDeliveryService.TryDeliverFromWarehouse(contractToDeliver.Id, designIdToDeliver, catalog);
                _contractActionMessage = result.Message;
                if (result.IsSuccess)
                {
                    _selectedWarehouseDesignByContractId.Remove(contractToDeliver.Id);
                    _contractActionMessage = $"{result.Message} Revenue booked: {FormatCurrency(contractIncomeToBook)}.";
                    _deliveredContractProfitById[contractToDeliver.Id] = contractProfitLossToBook;

                    if (_accountingSystem is not null && _productionFactory is not null)
                    {
                        var inventoryByDesign = _factoryAccountingCalculator.CalculateInventoryValueByDesign(
                            _productionFactory.WarehouseUnitsByDesign,
                            catalog);
                        _accountingSystem.RegisterContractIncome(
                            _elapsedMonths,
                            _currentDate,
                            contractIncomeToBook,
                            inventoryByDesign);
                    }
                }
            }
        }

        if (_contractBoard.DeliveredContracts.Count == 0)
        {
            GUILayout.Label("No delivered contracts yet.");
        }
        else
        {
            DrawSection("Delivered Contracts");
            for (var index = _contractBoard.DeliveredContracts.Count - 1; index >= 0; index--)
            {
                var delivered = _contractBoard.DeliveredContracts[index];
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(delivered.Id, GUILayout.Width(150f));
                GUILayout.Label(delivered.CompanyName, GUILayout.Width(180f));
                GUILayout.Label($"{FormatUiInteger(delivered.QuantityUnits)} units", GUILayout.Width(110f));
                GUILayout.Label($"Revenue: {FormatCurrency(GetContractTotalPrice(delivered))}", GUILayout.Width(220f));
                var deliveredProfitLoss = _deliveredContractProfitById.TryGetValue(delivered.Id, out var value)
                    ? value
                    : 0d;
                DrawSignedCurrencyInline($"P/L: {FormatCurrency(deliveredProfitLoss)}", deliveredProfitLoss, 170f);
                GUILayout.EndHorizontal();
            }
        }

        if (_contractBoard.CanceledContracts.Count == 0)
        {
            return;
        }

        DrawSection("Canceled Contracts");
        for (var index = _contractBoard.CanceledContracts.Count - 1; index >= 0; index--)
        {
            var canceled = _contractBoard.CanceledContracts[index];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{canceled.Id} | {canceled.CompanyName}");
            GUILayout.Label($"Quantity: {FormatUiInteger(canceled.QuantityUnits)} units");
            GUILayout.Label($"Reason: {canceled.CancellationReason}");
            GUILayout.EndVertical();
        }
    }


    private static ContractUIState CreateDefaultGovernmentContractNotebookState()
    {
        return new ContractUIState
        {
            IsOpen = false,
            Position = new Vector2(120f, -120f),
            Size = new Vector2(760f, 460f),
        };
    }

    private static ContractUIState CloneGovernmentContractNotebookState(ContractUIState? state)
    {
        var source = state ?? CreateDefaultGovernmentContractNotebookState();
        var size = source.Size;
        if (size.x <= 0f || size.y <= 0f)
        {
            size = new Vector2(760f, 460f);
        }

        return new ContractUIState
        {
            IsOpen = source.IsOpen,
            Position = source.Position,
            Size = size,
        };
    }

    private static NotebookWindowSaveState CreateNotebookWindowSaveState(ContractUIState? state)
    {
        var normalized = CloneGovernmentContractNotebookState(state);
        return new NotebookWindowSaveState
        {
            IsOpen = normalized.IsOpen,
            PositionX = normalized.Position.x,
            PositionY = normalized.Position.y,
            SizeX = normalized.Size.x,
            SizeY = normalized.Size.y,
        };
    }

    private static ContractUIState ReadNotebookWindowSaveState(NotebookWindowSaveState? state)
    {
        if (state is null)
        {
            return CreateDefaultGovernmentContractNotebookState();
        }

        var position = new Vector2(
            state.Position?.X ?? state.PositionX,
            state.Position?.Y ?? state.PositionY);
        var size = new Vector2(
            state.Size?.X ?? state.SizeX,
            state.Size?.Y ?? state.SizeY);

        return CloneGovernmentContractNotebookState(new ContractUIState
        {
            IsOpen = state.IsOpen,
            Position = position,
            Size = size,
        });
    }

    private void EnsureGovernmentContractNotebookWindow()
    {
        if (_governmentContractNotebookWindow is not null)
        {
            return;
        }

        _governmentContractNotebookWindow = GovernmentContractNotebookWindow.CreateRuntimeUi(
            transform,
            CloneGovernmentContractNotebookState(_governmentContractNotebookState));
    }

    private void ApplyGovernmentContractNotebookState()
    {
        if (_governmentContractNotebookWindow is null)
        {
            if (!_governmentContractNotebookState.IsOpen)
            {
                return;
            }

            EnsureGovernmentContractNotebookWindow();
        }

        _governmentContractNotebookWindow?.ApplyState(CloneGovernmentContractNotebookState(_governmentContractNotebookState));
    }

    private void ResetGovernmentContractNotebookWindow()
    {
        _governmentContractNotebookState = CreateDefaultGovernmentContractNotebookState();
        EnsureGovernmentContractNotebookWindow();
        ApplyGovernmentContractNotebookState();
        RefreshGovernmentContractNotebookWindowContent();
    }

    private void OpenGovernmentContractNotebookWindow()
    {
        EnsureGovernmentContractNotebookWindow();
        RefreshGovernmentContractNotebookWindowContent();
        _governmentContractNotebookWindow?.Open();
        if (_governmentContractNotebookWindow is not null)
        {
            _governmentContractNotebookState = CloneGovernmentContractNotebookState(_governmentContractNotebookWindow.CaptureState());
        }
    }

    private void CloseGovernmentContractNotebookWindow()
    {
        if (_governmentContractNotebookWindow is null)
        {
            return;
        }

        _governmentContractNotebookWindow.Close();
        _governmentContractNotebookState = CloneGovernmentContractNotebookState(_governmentContractNotebookWindow.CaptureState());
    }

    private void RefreshGovernmentContractNotebookWindowContent()
    {
        if (_governmentContractNotebookWindow is null)
        {
            return;
        }

        var activeContract = _governmentContractBoard?.ActiveContract;
        if (activeContract is null)
        {
            _governmentContractNotebookWindow.SetContent(
                "Government Contract Notebook",
                "No active government contract.");
            return;
        }

        var requirements = activeContract.Requirements;
        var categoryProfile = AircraftCategoryCatalog.GetProfile(requirements.RequiredAircraftType);
        var lines = new List<string>
        {
            $"Contract: {activeContract.ContractId}",
            $"Status: {activeContract.Status}",
            $"Generated: {FormatSimulationDate(activeContract.GeneratedOn)}",
            $"Availability Ends: {FormatSimulationDate(activeContract.AvailabilityExpiresOn)}",
        };

        if (activeContract.AcceptedOn.HasValue)
        {
            lines.Add($"Accepted On: {FormatSimulationDate(activeContract.AcceptedOn.Value)}");
        }

        if (activeContract.EvaluationDueOn.HasValue)
        {
            lines.Add($"Evaluation Due: {FormatSimulationDate(activeContract.EvaluationDueOn.Value)}");
        }

        if (activeContract.AcceptedSubmission is not null)
        {
            lines.Add($"Selected Aircraft: {activeContract.AcceptedSubmission.DisplayName} ({activeContract.AcceptedSubmission.DesignId})");
        }

        if (activeContract.UpfrontFundingGranted > 0d)
        {
            lines.Add($"Upfront Funding: {FormatCurrency(activeContract.UpfrontFundingGranted)}");
        }

        lines.Add(string.Empty);
        lines.Add("Requirements");
        lines.Add($"- Aircraft Type: {categoryProfile.DisplayName}");
        lines.Add($"- Speed Altitude Band: {requirements.SpeedAltitudeBand}");
        lines.Add($"- Minimum Max Speed: {FormatUiNumber(requirements.MinimumMaximumSpeedMetersPerSecond * 3.6d, "0.0")} km/h");
        lines.Add($"- Minimum Range: {FormatUiNumber(requirements.MinimumRangeKilometers, "0.0")} km");
        lines.Add($"- Minimum Payload: {FormatUiNumber(requirements.MinimumPayloadKilograms, "0.0")} kg");
        lines.Add($"- Minimum Reliability: {FormatUiNumber(requirements.MinimumReliability, "0.000")}");
        lines.Add($"- Minimum Crew: {FormatUiInteger(requirements.MinimumCrewSize)}");
        lines.Add($"- Maximum Runway Length: {FormatUiNumber(requirements.MaximumRequiredRunwayLengthMeters, "0.0")} m");

        _governmentContractNotebookWindow.SetContent(
            $"Government Contract Notebook | {activeContract.ContractId}",
            string.Join(Environment.NewLine, lines));
    }
    private void DrawGovernmentContractsTab()
    {
        DrawSection("Government Contracts (M9)");
        if (_governmentContractBoard is null || _productionFactory is null)
        {
            DrawError("Government contract system is not initialized.");
            return;
        }

        GUILayout.Label("A single government contract is generated each January.");
        GUILayout.Label("Accepting grants immediate upfront funding (75% of development cost).");
        GUILayout.Label("Delivery contracts auto-ship monthly from warehouse stock.");

        var activeContract = _governmentContractBoard.ActiveContract;
        var activeDelivery = _governmentContractBoard.ActiveDeliveryContract;
        GUILayout.Label($"Active Contract: {(activeContract is null ? "None" : $"{activeContract.ContractId} ({activeContract.Status})")}");
        GUILayout.Label($"Active Delivery: {(activeDelivery is null ? "None" : activeDelivery.DeliveryId)}");
        GUILayout.Label($"Available Designs: {FormatUiInteger(_savedAircraftDesigns.Count)}");
        GUILayout.Label($"Warehouse Units: {FormatUiInteger(_productionFactory.WarehouseUnitsByDesign.Values.Sum())}");
        GUILayout.Label(
            $"Expired: {FormatUiInteger(_governmentContractBoard.ExpiredContracts.Count)} | " +
            $"Evaluated: {FormatUiInteger(_governmentContractBoard.EvaluatedContracts.Count)} | " +
            $"Closed Deliveries: {FormatUiInteger(_governmentContractBoard.ClosedDeliveryContracts.Count)}");

        if (!string.IsNullOrWhiteSpace(_governmentContractActionMessage))
        {
            GUILayout.Label(_governmentContractActionMessage);
        }

        if (!string.IsNullOrWhiteSpace(_governmentDeliveryActionMessage))
        {
            GUILayout.Label(_governmentDeliveryActionMessage);
        }

        GUILayout.BeginHorizontal();
        GUI.enabled = activeContract is not null;
        if (GUILayout.Button("Notebook", GUILayout.Width(140f), GUILayout.Height(30f)))
        {
            OpenGovernmentContractNotebookWindow();
        }

        GUI.enabled = true;
        if (GUILayout.Button("Go To Airframe + Type Tab", GUILayout.Height(30f)))
        {
            _tab = 1;
        }

        GUILayout.EndHorizontal();
    }

    private void DrawGovernmentContractsReport()
    {
        DrawSection("Government Contract Board");
        if (_governmentContractBoard is null || _productionFactory is null)
        {
            DrawError("Government contract system is not initialized.");
            return;
        }

        var activeContract = _governmentContractBoard.ActiveContract;
        if (activeContract is null)
        {
            GUILayout.Label("No active government contract.");
            GUILayout.Label("A new one appears every January when no contract is active.");
        }
        else
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{activeContract.ContractId} | Status: {activeContract.Status}");
            GUILayout.Label($"Generated: {FormatSimulationDate(activeContract.GeneratedOn)}");
            GUILayout.Label($"Availability Ends: {FormatSimulationDate(activeContract.AvailabilityExpiresOn)}");
            if (activeContract.AcceptedOn.HasValue)
            {
                GUILayout.Label($"Accepted On: {FormatSimulationDate(activeContract.AcceptedOn.Value)}");
            }

            if (activeContract.EvaluationDueOn.HasValue)
            {
                GUILayout.Label($"Evaluation Due: {FormatSimulationDate(activeContract.EvaluationDueOn.Value)}");
            }

            if (activeContract.AcceptedSubmission is not null)
            {
                GUILayout.Label($"Selected Aircraft: {activeContract.AcceptedSubmission.DisplayName} ({activeContract.AcceptedSubmission.DesignId})");
            }

            if (activeContract.UpfrontFundingGranted > 0d)
            {
                DrawMetric("Upfront Funding Granted", activeContract.UpfrontFundingGranted, "0.00");
            }

            DrawGovernmentRequirements(activeContract.Requirements);
            GUILayout.EndVertical();

            if (GUILayout.Button("Notebook", GUILayout.Width(140f), GUILayout.Height(26f)))
            {
                OpenGovernmentContractNotebookWindow();
            }

            if (activeContract.Status == GovernmentContractStatus.Available)
            {
                DrawSection("Submission Candidates");
                var assessments = BuildGovernmentSubmissionAssessments();
                if (assessments.Count == 0)
                {
                    GUILayout.Label("No saved aircraft designs available. Save aircraft in Airframe + Type tab first.");
                }
                else
                {
                    for (var index = 0; index < assessments.Count; index++)
                    {
                        var assessment = assessments[index];
                        var candidate = assessment.Candidate;
                        var compliance = assessment.Compliance;

                        var previousColor = GUI.contentColor;
                        if (!compliance.IsSelectableInUi)
                        {
                            GUI.contentColor = new Color(1f, 0.45f, 0.45f, 1f);
                        }

                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label($"{candidate.DisplayName} ({candidate.DesignId})");
                        GUILayout.Label($"Lifecycle: {candidate.LifecycleStage}");
                        DrawMetric("Development Cost", candidate.Metrics.DevelopmentCost, "0.00");
                        DrawMetric("Reliability", candidate.Metrics.Reliability, "0.000");
                        DrawMetric("Range (km)", candidate.Metrics.RangeKilometers, "0.0");
                        DrawMetric("Payload (kg)", candidate.Metrics.PayloadKilograms, "0.0");
                        DrawMetric("Runway Length (m)", candidate.Metrics.RequiredRunwayLengthMeters, "0.0");

                        GUI.enabled = compliance.IsSelectableInUi;
                        if (GUILayout.Button("Accept Government Contract", GUILayout.Height(26f)))
                        {
                            var acceptance = _governmentContractBoard.TryAcceptActiveContract(_currentDate, candidate);
                            _governmentContractActionMessage = acceptance.Message;

                            if (acceptance.IsAccepted
                                && acceptance.UpfrontFundingGranted > 0d
                                && _accountingSystem is not null)
                            {
                                var fullCatalog = BuildProductionCatalog(includeOnlyResearched: false);
                                var inventoryByDesign = _factoryAccountingCalculator.CalculateInventoryValueByDesign(
                                    _productionFactory.WarehouseUnitsByDesign,
                                    fullCatalog);
                                _accountingSystem.RegisterContractIncome(
                                    _elapsedMonths,
                                    _currentDate,
                                    acceptance.UpfrontFundingGranted,
                                    inventoryByDesign);
                                _governmentContractActionMessage =
                                    $"{acceptance.Message} Revenue booked: {FormatCurrency(acceptance.UpfrontFundingGranted)}.";
                            }

                            if (acceptance.IsAccepted)
                            {
                                OpenGovernmentContractNotebookWindow();
                            }

                            GUI.enabled = true;
                            GUI.contentColor = previousColor;
                            GUILayout.EndVertical();
                            break;
                        }

                        GUI.enabled = true;

                        if (!compliance.IsSelectableInUi)
                        {
                            DrawError("Not eligible for submission.");
                            for (var unmetIndex = 0; unmetIndex < compliance.UnmetRequirements.Count; unmetIndex++)
                            {
                                GUILayout.Label($"- {compliance.UnmetRequirements[unmetIndex]}");
                            }
                        }
                        else
                        {
                            GUILayout.Label("Meets all requirements.");
                        }

                        GUILayout.EndVertical();
                        GUI.contentColor = previousColor;
                    }
                }
            }
        }

        DrawSection("Latest Evaluation");
        if (_governmentContractBoard.EvaluatedContracts.Count == 0)
        {
            GUILayout.Label("No evaluated government contracts yet.");
        }
        else
        {
            var latest = _governmentContractBoard.EvaluatedContracts[^1];
            var evaluation = latest.EvaluationResult;
            if (evaluation is not null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{latest.ContractId} | {(evaluation.IsSuccess ? "SUCCESS" : "FAILED")}");
                DrawMetric("Final Probability", evaluation.FinalProbability * 100d, "0.00");
                DrawMetric("Random Roll", evaluation.RandomRoll * 100d, "0.00");
                DrawMetric("Bonus Probability", evaluation.BonusProbability * 100d, "0.00");
                for (var index = 0; index < evaluation.ParameterBonuses.Count; index++)
                {
                    var parameterBonus = evaluation.ParameterBonuses[index];
                    GUILayout.Label(
                        $"{parameterBonus.Parameter}: required {FormatUiNumber(parameterBonus.RequiredValue, "0.###")}, " +
                        $"actual {FormatUiNumber(parameterBonus.ActualValue, "0.###")}, " +
                        $"bonus +{FormatUiNumber(parameterBonus.BonusValue * 100d, "0.00")}%");
                }

                GUILayout.EndVertical();
            }
        }

        DrawSection("Delivery Contracts");
        var activeDelivery = _governmentContractBoard.ActiveDeliveryContract;
        if (activeDelivery is null)
        {
            GUILayout.Label("No active government delivery contract.");
        }
        else
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{activeDelivery.DeliveryId} | Source: {ResolveAircraftDesignName(activeDelivery.AircraftDesignId)}");
            GUILayout.Label($"Status: {activeDelivery.Status}");
            GUILayout.Label($"Active Window: {FormatSimulationDate(activeDelivery.StartedOn)} -> {FormatSimulationDate(activeDelivery.EndsOn)}");
            DrawMetric("Required Units", activeDelivery.TotalUnitsRequired, "0");
            DrawMetric("Delivered Units", activeDelivery.DeliveredUnits, "0");
            DrawMetric("Remaining Units", activeDelivery.RemainingUnits, "0");
            DrawMetric("Unit Price", activeDelivery.UnitPrice, "0.00");
            DrawMetric("Total Contract Value", activeDelivery.TotalContractValue, "0.00");
            GUILayout.EndVertical();
        }

        if (_governmentContractBoard.ClosedDeliveryContracts.Count > 0)
        {
            DrawSection("Closed Delivery History");
            for (var index = _governmentContractBoard.ClosedDeliveryContracts.Count - 1; index >= 0; index--)
            {
                var closed = _governmentContractBoard.ClosedDeliveryContracts[index];
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(closed.DeliveryId, GUILayout.Width(220f));
                GUILayout.Label($"Status: {closed.Status}", GUILayout.Width(130f));
                GUILayout.Label($"{FormatUiInteger(closed.DeliveredUnits)}/{FormatUiInteger(closed.TotalUnitsRequired)} units");
                GUILayout.EndHorizontal();
            }
        }
    }

    private IReadOnlyList<GovernmentContractSubmissionAssessment> BuildGovernmentSubmissionAssessments()
    {
        if (_governmentContractBoard is null)
        {
            return Array.Empty<GovernmentContractSubmissionAssessment>();
        }

        var submissions = new List<GovernmentContractSubmissionCandidate>(_savedAircraftDesigns.Count);
        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var savedDesign = _savedAircraftDesigns[index];
            var lifecycleStage = IsAircraftDesignReadyForProduction(savedDesign)
                ? WrightTycoon.Core.GovernmentContracts.AircraftLifecycleStage.Completed
                : WrightTycoon.Core.GovernmentContracts.AircraftLifecycleStage.InDevelopment;

            try
            {
                var submission = GovernmentContractSubmissionCandidateFactory.FromFactoryCatalogItem(
                    savedDesign.CatalogItem,
                    lifecycleStage);
                submissions.Add(submission);
            }
            catch (Exception ex)
            {
                _governmentContractActionMessage =
                    $"Failed to evaluate '{savedDesign.Name}' for government submission: {ex.Message}";
            }
        }

        return _governmentContractBoard.GetSubmissionAssessments(submissions);
    }

    private void DrawGovernmentRequirements(GovernmentContractRequirements requirements)
    {
        DrawSection("Requirements");
        var categoryProfile = AircraftCategoryCatalog.GetProfile(requirements.RequiredAircraftType);
        GUILayout.Label($"Aircraft Type: {categoryProfile.DisplayName}");
        GUILayout.Label($"Maximum Speed Band: {requirements.SpeedAltitudeBand}");
        DrawMetric("Minimum Max Speed (km/h)", requirements.MinimumMaximumSpeedMetersPerSecond * 3.6d, "0.0");
        DrawMetric("Minimum Range (km)", requirements.MinimumRangeKilometers, "0.0");
        DrawMetric("Minimum Payload (kg)", requirements.MinimumPayloadKilograms, "0.0");
        DrawMetric("Minimum Reliability", requirements.MinimumReliability, "0.000");
        DrawMetric("Minimum Crew", requirements.MinimumCrewSize, "0");
        DrawMetric("Maximum Runway Length (m)", requirements.MaximumRequiredRunwayLengthMeters, "0.0");
    }

    private void DrawResearchTab()
    {
        DrawSection("Research (M4)");
        if (_researchBoard is null)
        {
            DrawError("Research board is not initialized.");
            return;
        }

        var proposed = GetVisibleResearchProjectsByStatus(ResearchStatus.Proposed);
        var ongoing = GetVisibleResearchProjectsByStatus(ResearchStatus.Ongoing);
        var completed = GetVisibleResearchProjectsByStatus(ResearchStatus.Completed);

        GUILayout.Label("Approve proposed engines/families/types and tune research modifiers per proposal. Ongoing projects advance every month.");
        DrawMetric("Proposed", proposed.Count, "0");
        DrawMetric("Ongoing", ongoing.Count, "0");
        DrawMetric("Completed", completed.Count, "0");

        _researchSubTab = Mathf.Clamp(_researchSubTab, 0, _researchSubTabs.Length - 1);
        _researchSubTab = GUILayout.Toolbar(_researchSubTab, _researchSubTabs, GUILayout.Height(30f));

        if (!string.IsNullOrWhiteSpace(_researchActionMessage))
        {
            GUILayout.Label(_researchActionMessage);
        }

        if (_researchSubTab == 0)
        {
            DrawResearchCurrentScreen(isDetailed: false);
        }
        else
        {
            DrawResearchApprovalScreen(isDetailed: false);
        }
    }

    private void DrawResearchReport()
    {
        DrawSection("Research Board");
        if (_researchBoard is null)
        {
            DrawError("Research board is not initialized.");
            return;
        }

        var nextMonthBookedExpense = _researchBoard.GetBookedResearchExpenseForMonth(_elapsedMonths + 1);
        DrawMetric("Booked R&D (next month)", nextMonthBookedExpense, "0.00");

        if (_researchSubTab == 0)
        {
            DrawResearchCurrentScreen(isDetailed: true);
        }
        else
        {
            DrawResearchApprovalScreen(isDetailed: true);
        }
    }

    private void DrawResearchCurrentScreen(bool isDetailed)
    {
        if (_researchBoard is null)
        {
            return;
        }

        DrawSection("Current Research");
        var ongoing = GetVisibleResearchProjectsByStatus(ResearchStatus.Ongoing);
        if (ongoing.Count == 0)
        {
            GUILayout.Label("No ongoing research.");
        }
        else
        {
            for (var index = 0; index < ongoing.Count; index++)
            {
                var project = ongoing[index];
                var displayName = ResolveResearchProjectDisplayName(project);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{GetResearchSubjectLabel(project.SubjectType)}: {displayName}");
                GUILayout.Label($"Progress: {FormatUiInteger(project.ProgressMonths)}/{FormatUiInteger(project.ResearchTimeMonths)} months");
                DrawHorizontalBar(project.ProgressMonths, project.ResearchTimeMonths, new Color(0.65f, 0.82f, 0.53f, 1f));
                if (isDetailed)
                {
                    DrawMetric("Research Cost", project.ResearchCost, "0.00");
                    DrawMetric("Complexity (%)", project.ComplexityNormalized * 100d, "0.0");
                    DrawMetric("Remaining Months", project.RemainingMonths, "0");
                    GUILayout.Label($"ID: {project.SubjectId}");
                }

                GUILayout.EndVertical();
            }
        }

        DrawSection("Completed Research");
        var completed = GetVisibleResearchProjectsByStatus(ResearchStatus.Completed);
        if (completed.Count == 0)
        {
            GUILayout.Label("No completed research yet.");
            return;
        }

        for (var index = completed.Count - 1; index >= 0; index--)
        {
            var project = completed[index];
            var displayName = ResolveResearchProjectDisplayName(project);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(GetResearchSubjectLabel(project.SubjectType), GUILayout.Width(120f));
            GUILayout.Label(displayName, GUILayout.Width(260f));
            GUILayout.Label($"Done ({FormatUiInteger(project.ResearchTimeMonths)} m)");
            GUILayout.EndHorizontal();
        }
    }

    private void DrawResearchApprovalScreen(bool isDetailed)
    {
        if (_researchBoard is null)
        {
            return;
        }

        DrawSection("For Approval");
        var proposed = GetVisibleResearchProjectsByStatus(ResearchStatus.Proposed);
        if (proposed.Count == 0)
        {
            GUILayout.Label("No pending proposals for saved designs.");
            return;
        }

        if (GUILayout.Button("Approve All", GUILayout.Height(26f)))
        {
            for (var index = 0; index < proposed.Count; index++)
            {
                TryApproveResearchProject(proposed[index]);
            }
        }

        for (var index = 0; index < proposed.Count; index++)
        {
            var project = proposed[index];
            var displayName = ResolveResearchProjectDisplayName(project);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Research Subject: {GetResearchSubjectLabel(project.SubjectType)}");
            GUILayout.Label($"{GetResearchNameFieldLabel(project.SubjectType)}: {displayName}");
            DrawMetric("Research Cost", project.ResearchCost, "0.00");
            DrawMetric("Research Time (months)", project.ResearchTimeMonths, "0");
            DrawMetric("Complexity (%)", project.ComplexityNormalized * 100d, "0.0");
            DrawResearchModifierEditor(project);
            if (isDetailed)
            {
                GUILayout.Label($"Research ID: {project.SubjectId}");
            }

            if (GUILayout.Button($"Approve {GetResearchSubjectLabel(project.SubjectType)}", GUILayout.Height(24f)))
            {
                TryApproveResearchProject(project);
            }

            GUILayout.EndVertical();
        }
    }

    private void TryApproveResearchProject(ResearchProject project)
    {
        if (_researchBoard is null)
        {
            return;
        }

        var bookingMonthIndex = _elapsedMonths + 1;
        var bookedDate = _currentDate.NextMonth();
        if (_researchBoard.TryApproveProposal(
            project.SubjectType,
            project.SubjectId,
            bookingMonthIndex,
            out _,
            out var message))
        {
            _researchActionMessage = $"{message} Expense booked in {FormatSimulationDate(bookedDate)}.";
            return;
        }

        _researchActionMessage = message;
    }


    private IReadOnlyList<ResearchProject> GetVisibleResearchProjectsByStatus(ResearchStatus status)
    {
        if (_researchBoard is null)
        {
            return Array.Empty<ResearchProject>();
        }

        return _researchBoard
            .GetProjectsByStatus(status)
            .Where(IsResearchProjectVisible)
            .ToArray();
    }

    private bool IsResearchProjectVisible(ResearchProject project)
    {
        if (project is null)
        {
            return false;
        }

        return project.SubjectType switch
        {
            ResearchSubjectType.Engine =>
                _savedEngines.Any(saved => string.Equals(saved.ResearchId, project.SubjectId, StringComparison.Ordinal))
                || _savedAircraftDesigns.Any(saved => string.Equals(saved.EngineResearchId, project.SubjectId, StringComparison.Ordinal)),
            ResearchSubjectType.AircraftFamily =>
                _savedFamilies.Any(saved => string.Equals(saved.FamilyId, project.SubjectId, StringComparison.Ordinal))
                || _savedAircraftDesigns.Any(saved => string.Equals(saved.FamilyResearchId, project.SubjectId, StringComparison.Ordinal)),
            ResearchSubjectType.AircraftType =>
                _savedAircraftDesigns.Any(saved => string.Equals(saved.TypeResearchId, project.SubjectId, StringComparison.Ordinal)),
            _ => false,
        };
    }

    private void DrawResearchModifierEditor(ResearchProject project)
    {
        if (project is null)
        {
            return;
        }

        var points = GetOrCreateResearchModifierPoints(project);
        var sliderIdSuffix = project.ResearchId;
        var previousReliability = points.ReliabilityPoints;
        var previousProductionOptimization = points.ProductionOptimizationPoints;
        var previousFastDevelopment = points.FastDevelopmentPoints;

        DrawSection($"{GetResearchSubjectLabel(project.SubjectType)} Research Modifiers");
        points.ReliabilityPoints = DrawIntSlider(
            $"Reliability (0-10) [{sliderIdSuffix}]",
            points.ReliabilityPoints,
            0,
            TypeResearchModifierPoints.MaxPointsPerCategory);
        points.ProductionOptimizationPoints = DrawIntSlider(
            $"Production Optimization (0-10) [{sliderIdSuffix}]",
            points.ProductionOptimizationPoints,
            0,
            TypeResearchModifierPoints.MaxPointsPerCategory);
        points.FastDevelopmentPoints = DrawIntSlider(
            $"Fast Development (0-10) [{sliderIdSuffix}]",
            points.FastDevelopmentPoints,
            0,
            TypeResearchModifierPoints.MaxPointsPerCategory);

        var effects = TypeResearchModifierCalculator.Calculate(points);
        GUILayout.Label(
            $"Selected Points: REL {FormatUiInteger(points.ReliabilityPoints)} | PROD {FormatUiInteger(points.ProductionOptimizationPoints)} | FAST {FormatUiInteger(points.FastDevelopmentPoints)}");
        DrawResearchModifierEffectPreview(project, points, effects);

        var changed = points.ReliabilityPoints != previousReliability
            || points.ProductionOptimizationPoints != previousProductionOptimization
            || points.FastDevelopmentPoints != previousFastDevelopment;
        if (!changed)
        {
            return;
        }

        _researchModifiersByResearchId[BuildResearchModifierId(project.SubjectType, project.SubjectId)] = points.Clone();
        if (project.SubjectType == ResearchSubjectType.AircraftType)
        {
            ApplyTypeResearchModifiersToSavedDesigns(project.SubjectId, points);
        }

        if (project.Status == ResearchStatus.Proposed)
        {
            TryRefreshResearchProposalQuote(project, points);
        }
    }

    private void DrawResearchModifierEffectPreview(
        ResearchProject project,
        TypeResearchModifierPoints points,
        TypeResearchModifierEffects effects)
    {
        if (project is null)
        {
            return;
        }

        switch (project.SubjectType)
        {
            case ResearchSubjectType.Engine:
                DrawEngineResearchModifierEffects(project.SubjectId, points, effects);
                break;
            case ResearchSubjectType.AircraftFamily:
                DrawFamilyResearchModifierEffects(project.SubjectId, points, effects);
                break;
            case ResearchSubjectType.AircraftType:
                DrawTypeResearchModifierEffects(project.SubjectId, points, effects);
                break;
        }
    }

    private void DrawEngineResearchModifierEffects(
        string researchId,
        TypeResearchModifierPoints points,
        TypeResearchModifierEffects effects)
    {
        if (!TryBuildSavedEngineSnapshot(researchId, out var engine))
        {
            return;
        }

        var baseQuote = _researchCalculator.CalculateEngineQuote(engine);
        var modifiedQuote = _researchCalculator.CalculateEngineQuote(engine, points);
        var modifiedReliability = ClampResearchPreviewReliability(engine.Performance.Reliability + effects.ReliabilityDelta);
        var modifiedProductionCost = System.Math.Max(0d, engine.Performance.Cost * effects.ProductionCostMultiplier);
        var modifiedProductionManDays = System.Math.Max(0d, engine.Performance.ProductionManDays * effects.ProductionManDaysMultiplier);

        DrawSection("Affected Parameters");
        DrawModifierValuePreview("Reliability", modifiedReliability - engine.Performance.Reliability, "0.00", modifiedReliability, "0.000");
        DrawModifierPercentagePreview(
            "Production Cost",
            (effects.ProductionCostMultiplier - 1d) * 100d,
            modifiedProductionCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Production MD",
            (effects.ProductionManDaysMultiplier - 1d) * 100d,
            modifiedProductionManDays,
            "0.0");
        DrawModifierPercentagePreview(
            "Research Cost",
            CalculatePercentageDelta(baseQuote.ResearchCost, modifiedQuote.ResearchCost),
            modifiedQuote.ResearchCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Research Time (months)",
            CalculatePercentageDelta(baseQuote.ResearchTimeMonths, modifiedQuote.ResearchTimeMonths),
            modifiedQuote.ResearchTimeMonths,
            "0");
    }

    private void DrawFamilyResearchModifierEffects(
        string familyResearchId,
        TypeResearchModifierPoints points,
        TypeResearchModifierEffects effects)
    {
        if (!TryGetSavedAircraftDesignByFamilyResearchId(familyResearchId, out var savedDesign))
        {
            return;
        }

        var aircraft = savedDesign.CatalogItem.Aircraft;
        var baseQuote = _researchCalculator.CalculateFamilyQuote(aircraft);
        var modifiedQuote = _researchCalculator.CalculateFamilyQuote(aircraft, points);
        var modifiedReliability = ClampResearchPreviewReliability(aircraft.Performance.OverallReliability + effects.ReliabilityDelta);
        var modifiedProductionCost = System.Math.Max(0d, aircraft.Performance.TotalProductionCost * effects.ProductionCostMultiplier);
        var modifiedProductionManDays = System.Math.Max(0d, aircraft.Performance.TotalProductionManDays * effects.ProductionManDaysMultiplier);

        DrawSection("Affected Parameters");
        DrawModifierValuePreview(
            "Reliability",
            modifiedReliability - aircraft.Performance.OverallReliability,
            "0.00",
            modifiedReliability,
            "0.000");
        DrawModifierPercentagePreview(
            "Production Cost",
            (effects.ProductionCostMultiplier - 1d) * 100d,
            modifiedProductionCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Production MD",
            (effects.ProductionManDaysMultiplier - 1d) * 100d,
            modifiedProductionManDays,
            "0.0");
        DrawModifierPercentagePreview(
            "Research Cost",
            CalculatePercentageDelta(baseQuote.ResearchCost, modifiedQuote.ResearchCost),
            modifiedQuote.ResearchCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Research Time (months)",
            CalculatePercentageDelta(baseQuote.ResearchTimeMonths, modifiedQuote.ResearchTimeMonths),
            modifiedQuote.ResearchTimeMonths,
            "0");
    }

    private void DrawTypeResearchModifierEffects(
        string typeResearchId,
        TypeResearchModifierPoints points,
        TypeResearchModifierEffects effects)
    {
        if (!TryBuildBaseAircraftForTypeResearch(typeResearchId, out var aircraft))
        {
            return;
        }

        var baseQuote = _researchCalculator.CalculateTypeQuote(aircraft);
        var modifiedQuote = _researchCalculator.CalculateTypeQuote(aircraft, points);
        var modifiedReliability = ClampResearchPreviewReliability(aircraft.Performance.OverallReliability + effects.ReliabilityDelta);
        var modifiedProductionCost = System.Math.Max(0d, aircraft.Performance.TotalProductionCost * effects.ProductionCostMultiplier);
        var modifiedProductionManDays = System.Math.Max(0d, aircraft.Performance.TotalProductionManDays * effects.ProductionManDaysMultiplier);

        DrawSection("Affected Parameters");
        DrawModifierValuePreview(
            "Reliability",
            modifiedReliability - aircraft.Performance.OverallReliability,
            "0.00",
            modifiedReliability,
            "0.000");
        DrawModifierPercentagePreview(
            "Production Cost",
            (effects.ProductionCostMultiplier - 1d) * 100d,
            modifiedProductionCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Production MD",
            (effects.ProductionManDaysMultiplier - 1d) * 100d,
            modifiedProductionManDays,
            "0.0");
        DrawModifierPercentagePreview(
            "Research Cost",
            CalculatePercentageDelta(baseQuote.ResearchCost, modifiedQuote.ResearchCost),
            modifiedQuote.ResearchCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Research Time (months)",
            CalculatePercentageDelta(baseQuote.ResearchTimeMonths, modifiedQuote.ResearchTimeMonths),
            modifiedQuote.ResearchTimeMonths,
            "0");
    }

    private bool TryBuildSavedEngineSnapshot(string researchId, out Engine engine)
    {
        engine = null!;
        if (_engineBuilder is null)
        {
            return false;
        }

        for (var index = 0; index < _savedEngines.Count; index++)
        {
            var savedEngine = _savedEngines[index];
            if (!string.Equals(savedEngine.ResearchId, researchId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                engine = _engineBuilder.Build(savedEngine.Definition, savedEngine.Parameters.Clone());
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private bool TryGetSavedAircraftDesignByFamilyResearchId(string familyResearchId, out SavedAircraftDesign savedDesign)
    {
        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var candidate = _savedAircraftDesigns[index];
            if (string.Equals(candidate.FamilyResearchId, familyResearchId, StringComparison.Ordinal))
            {
                savedDesign = candidate;
                return true;
            }
        }

        savedDesign = null!;
        return false;
    }

    private bool TryGetSavedAircraftDesignByTypeResearchId(string typeResearchId, out SavedAircraftDesign savedDesign)
    {
        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var candidate = _savedAircraftDesigns[index];
            if (string.Equals(candidate.TypeResearchId, typeResearchId, StringComparison.Ordinal))
            {
                savedDesign = candidate;
                return true;
            }
        }

        savedDesign = null!;
        return false;
    }

    private bool TryBuildBaseAircraftForTypeResearch(string typeResearchId, out Aircraft aircraft)
    {
        aircraft = null!;
        if (!TryGetSavedAircraftDesignByTypeResearchId(typeResearchId, out var savedDesign))
        {
            return false;
        }

        if (_aircraftFactory is null)
        {
            aircraft = savedDesign.CatalogItem.Aircraft;
            return true;
        }

        var existingType = savedDesign.CatalogItem.Aircraft.TypeDefinition;
        var baselineType = new AircraftTypeDefinition
        {
            Id = existingType.Id,
            FamilyId = existingType.FamilyId,
            DisplayName = existingType.DisplayName,
            EngineDelta = existingType.EngineDelta,
            AirframeDelta = existingType.AirframeDelta,
            ResearchModifiers = new TypeResearchModifierPoints(),
            RequiredLineToolLevel = existingType.RequiredLineToolLevel,
        };

        try
        {
            aircraft = _aircraftFactory.CreateAircraft(savedDesign.CatalogItem.Aircraft.FamilyDefinition, baselineType);
            return true;
        }
        catch
        {
            aircraft = savedDesign.CatalogItem.Aircraft;
            return true;
        }
    }

    private static void DrawModifierValuePreview(
        string label,
        double deltaValue,
        string deltaFormat,
        double resultValue,
        string resultFormat)
    {
        GUILayout.Label($"{label} [{FormatSignedModifierDelta(deltaValue, deltaFormat)} = {FormatUiNumber(resultValue, resultFormat)}]");
    }

    private static void DrawModifierPercentagePreview(
        string label,
        double deltaPercent,
        double resultValue,
        string resultFormat)
    {
        GUILayout.Label($"{label} [{FormatSignedModifierDelta(deltaPercent, "0.0", "%")} = {FormatUiNumber(resultValue, resultFormat)}]");
    }

    private static string FormatSignedModifierDelta(double value, string format, string suffix = "")
    {
        var sign = value >= 0d ? "+" : string.Empty;
        return $"{sign}{FormatUiNumber(value, format)}{suffix}";
    }

    private static double CalculatePercentageDelta(double baseValue, double resultValue)
    {
        if (System.Math.Abs(baseValue) <= 0.000001d)
        {
            return 0d;
        }

        return ((resultValue / baseValue) - 1d) * 100d;
    }

    private static double ClampResearchPreviewReliability(double value) =>
        System.Math.Max(0d, System.Math.Min(0.995d, value));

    private bool TryRefreshResearchProposalQuote(ResearchProject project, TypeResearchModifierPoints? overridePoints = null)
    {
        if (_researchBoard is null || project is null)
        {
            return false;
        }

        var points = overridePoints ?? GetResearchModifierPoints(project.SubjectType, project.SubjectId);
        if (!TryBuildResearchQuote(project, points, out var displayName, out var quote))
        {
            return false;
        }

        _researchBoard.UpsertProposal(project.SubjectType, project.SubjectId, displayName, quote);
        return true;
    }

    private bool TryBuildResearchQuote(
        ResearchProject project,
        TypeResearchModifierPoints modifierPoints,
        out string displayName,
        out ResearchQuote quote)
    {
        displayName = ResolveResearchProjectDisplayName(project);
        quote = default;
        if (project is null)
        {
            return false;
        }

        if (project.SubjectType == ResearchSubjectType.Engine)
        {
            if (_engineBuilder is null)
            {
                return false;
            }

            for (var index = 0; index < _savedEngines.Count; index++)
            {
                var savedEngine = _savedEngines[index];
                if (!string.Equals(savedEngine.ResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    var engineSnapshot = _engineBuilder.Build(savedEngine.Definition, savedEngine.Parameters.Clone());
                    displayName = savedEngine.Name;
                    quote = _researchCalculator.CalculateEngineQuote(engineSnapshot, modifierPoints);
                    return true;
                }
                catch (Exception ex)
                {
                    _researchActionMessage = $"Failed to refresh engine quote for '{savedEngine.Name}': {ex.Message}";
                    return false;
                }
            }

            return false;
        }

        if (project.SubjectType == ResearchSubjectType.AircraftFamily)
        {
            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (!string.Equals(savedDesign.FamilyResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                displayName = savedDesign.CatalogItem.Aircraft.FamilyDefinition.DisplayName;
                quote = _researchCalculator.CalculateFamilyQuote(savedDesign.CatalogItem.Aircraft, modifierPoints);
                return true;
            }

            return false;
        }

        if (project.SubjectType == ResearchSubjectType.AircraftType)
        {
            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (!string.Equals(savedDesign.TypeResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                displayName = savedDesign.CatalogItem.Aircraft.TypeDefinition.DisplayName;
                quote = _researchCalculator.CalculateTypeQuote(savedDesign.CatalogItem.Aircraft, modifierPoints);
                return true;
            }

            return false;
        }

        return false;
    }

    private static string BuildResearchModifierId(ResearchSubjectType subjectType, string subjectId) =>
        ResearchBoard.BuildResearchId(subjectType, subjectId);

    private TypeResearchModifierPoints GetResearchModifierPoints(ResearchSubjectType subjectType, string subjectId)
    {
        var key = BuildResearchModifierId(subjectType, subjectId);
        if (_researchModifiersByResearchId.TryGetValue(key, out var points))
        {
            return points.Clone();
        }

        return new TypeResearchModifierPoints();
    }

    private TypeResearchModifierPoints GetOrCreateResearchModifierPoints(ResearchProject project)
    {
        if (project is null)
        {
            return new TypeResearchModifierPoints();
        }

        var key = BuildResearchModifierId(project.SubjectType, project.SubjectId);
        if (_researchModifiersByResearchId.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var points = new TypeResearchModifierPoints();
        _researchModifiersByResearchId[key] = points;
        return points;
    }

    private void EnsureResearchModifierEntriesForSavedDesigns()
    {
        for (var index = 0; index < _savedEngines.Count; index++)
        {
            var savedEngine = _savedEngines[index];
            var key = BuildResearchModifierId(ResearchSubjectType.Engine, savedEngine.ResearchId);
            if (_researchModifiersByResearchId.ContainsKey(key))
            {
                continue;
            }

            _researchModifiersByResearchId[key] = new TypeResearchModifierPoints();
        }

        for (var index = 0; index < _savedFamilies.Count; index++)
        {
            var savedFamily = _savedFamilies[index];
            var key = BuildResearchModifierId(ResearchSubjectType.AircraftFamily, savedFamily.FamilyId);
            if (_researchModifiersByResearchId.ContainsKey(key))
            {
                continue;
            }

            _researchModifiersByResearchId[key] = new TypeResearchModifierPoints();
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var savedDesign = _savedAircraftDesigns[index];
            var familyKey = BuildResearchModifierId(ResearchSubjectType.AircraftFamily, savedDesign.FamilyResearchId);
            if (!_researchModifiersByResearchId.ContainsKey(familyKey))
            {
                _researchModifiersByResearchId[familyKey] = new TypeResearchModifierPoints();
            }

            var typeKey = BuildResearchModifierId(ResearchSubjectType.AircraftType, savedDesign.TypeResearchId);
            if (_researchModifiersByResearchId.ContainsKey(typeKey))
            {
                continue;
            }

            _researchModifiersByResearchId[typeKey] =
                savedDesign.CatalogItem.Aircraft.TypeDefinition.ResearchModifiers?.Clone()
                ?? new TypeResearchModifierPoints();
        }
    }

    private void ApplyTypeResearchModifiersToSavedDesigns(string typeSubjectId, TypeResearchModifierPoints points)
    {
        if (_aircraftFactory is null)
        {
            return;
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var savedDesign = _savedAircraftDesigns[index];
            if (!string.Equals(savedDesign.TypeResearchId, typeSubjectId, StringComparison.Ordinal))
            {
                continue;
            }

            var aircraft = savedDesign.CatalogItem.Aircraft;
            var existingType = aircraft.TypeDefinition;
            var updatedType = new AircraftTypeDefinition
            {
                Id = existingType.Id,
                FamilyId = existingType.FamilyId,
                DisplayName = existingType.DisplayName,
                EngineDelta = existingType.EngineDelta,
                AirframeDelta = existingType.AirframeDelta,
                ResearchModifiers = points.Clone(),
                RequiredLineToolLevel = existingType.RequiredLineToolLevel,
            };

            try
            {
                var rebuiltAircraft = _aircraftFactory.CreateAircraft(aircraft.FamilyDefinition, updatedType);
                var updatedCatalogItem = new FactoryAircraftTypeCatalogItem(
                    savedDesign.CatalogItem.DesignId,
                    savedDesign.CatalogItem.DisplayName,
                    rebuiltAircraft,
                    savedDesign.CatalogItem.RequiredFactoryToolLevel,
                    savedDesign.CatalogItem.RequiredLineToolLevel);

                _savedAircraftDesigns[index] = new SavedAircraftDesign(
                    savedDesign.Name,
                    updatedCatalogItem,
                    savedDesign.EngineResearchId,
                    savedDesign.FamilyResearchId,
                    savedDesign.TypeResearchId);
            }
            catch (Exception ex)
            {
                _researchActionMessage = $"Failed to apply type modifiers to '{savedDesign.Name}': {ex.Message}";
            }
        }
    }

    private string ResolveResearchProjectDisplayName(ResearchProject project)
    {
        if (project is null)
        {
            return string.Empty;
        }

        if (project.SubjectType == ResearchSubjectType.Engine)
        {
            for (var index = 0; index < _savedEngines.Count; index++)
            {
                var savedEngine = _savedEngines[index];
                if (string.Equals(savedEngine.ResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    return savedEngine.Name;
                }
            }

            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (string.Equals(savedDesign.EngineResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    return savedDesign.CatalogItem.Aircraft.Engine.Definition.DisplayName;
                }
            }
        }
        else if (project.SubjectType == ResearchSubjectType.AircraftFamily)
        {
            for (var index = 0; index < _savedFamilies.Count; index++)
            {
                var savedFamily = _savedFamilies[index];
                if (string.Equals(savedFamily.FamilyId, project.SubjectId, StringComparison.Ordinal))
                {
                    return savedFamily.Name;
                }
            }

            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (string.Equals(savedDesign.FamilyResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    return savedDesign.CatalogItem.Aircraft.FamilyDefinition.DisplayName;
                }
            }
        }
        else if (project.SubjectType == ResearchSubjectType.AircraftType)
        {
            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (string.Equals(savedDesign.TypeResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    return savedDesign.Name;
                }
            }
        }

        return project.DisplayName;
    }

    private static string GetResearchNameFieldLabel(ResearchSubjectType subjectType)
    {
        return subjectType switch
        {
            ResearchSubjectType.Engine => "Engine Name",
            ResearchSubjectType.AircraftFamily => "Family Name",
            ResearchSubjectType.AircraftType => "Type Name",
            _ => "Name",
        };
    }
    private static string GetResearchSubjectLabel(ResearchSubjectType subjectType)
    {
        return subjectType switch
        {
            ResearchSubjectType.Engine => "Engine",
            ResearchSubjectType.AircraftFamily => "Family",
            ResearchSubjectType.AircraftType => "Type",
            _ => subjectType.ToString(),
        };
    }
    private void DrawFinanceTab()
    {
        DrawSection("Accounting (M7)");
        if (_accountingSystem is null)
        {
            DrawError("Accounting system is not initialized.");
            return;
        }

        GUILayout.Label("Track cash, monthly fixed/variable costs (including R&D), income, inventory value and profit/loss.");
        DrawMetric("Current Cash", _accountingSystem.Cash, "0.00");
        DrawMetric("Recorded Months", _accountingSystem.GetHistory().Count, "0");

        _financeSubTab = Mathf.Clamp(_financeSubTab, 0, _financeSubTabs.Length - 1);
        _financeSubTab = GUILayout.Toolbar(_financeSubTab, _financeSubTabs, GUILayout.Height(30f));
    }

    private void DrawFinanceReport()
    {
        DrawSection("Financial Dashboard");
        if (_accountingSystem is null)
        {
            DrawError("Accounting system is not initialized.");
            return;
        }

        var history = _accountingSystem.GetHistory();
        DrawMetric("Cash", _accountingSystem.Cash, "0.00");

        if (history.Count == 0)
        {
            GUILayout.Label("No financial history yet. Press Next Month to process production costs.");
            return;
        }

        _financeSubTab = Mathf.Clamp(_financeSubTab, 0, _financeSubTabs.Length - 1);
        if (_financeSubTab == 0)
        {
            DrawSection("Income / Expense Timeline");
            DrawIncomeExpenseTimelineChart(history);
            return;
        }

        var lastMonth = history[history.Count - 1];
        DrawSection($"Last Month Details ({FormatSimulationDate(lastMonth.Date)})");
        DrawMetric("Income", lastMonth.Income, "0.00");
        DrawMetric("Fixed Costs", lastMonth.Costs.FixedCosts, "0.00");
        DrawMetric("Variable Costs", lastMonth.Costs.VariableCosts, "0.00");
        DrawSignedCurrencyMetric("Monthly Profit/Loss", lastMonth.MonthlyProfitLoss);
        DrawMetric("Inventory Value", lastMonth.InventoryValue, "0.00");

        DrawSection("Fixed Expenses");
        DrawMetric("Production Lines", lastMonth.Costs.FixedProductionLineExpenses, "0.00");

        DrawSection("Variable Expenses");
        DrawMetric("Engine Staff", lastMonth.Costs.VariableEngineStaffExpenses, "0.00");
        DrawMetric("Engine Build Costs", lastMonth.Costs.VariableEngineBuildExpenses, "0.00");
        DrawMetric("Plane Staff", lastMonth.Costs.VariablePlaneStaffExpenses, "0.00");
        DrawMetric("Plane Build Costs", lastMonth.Costs.VariablePlaneBuildExpenses, "0.00");
        DrawMetric("R&D", lastMonth.Costs.VariableResearchExpenses, "0.00");

        DrawSection("Items In Stock (Cost Basis)");
        if (lastMonth.InventoryByDesign.Count == 0)
        {
            GUILayout.Label("No inventory in stock.");
        }
        else
        {
            foreach (var entry in lastMonth.InventoryByDesign.OrderByDescending(item => item.Value.TotalValue))
            {
                var inventoryItem = entry.Value;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{ResolveAircraftDesignName(entry.Key)} ({ResolveAircraftCategoryName(entry.Key)})", GUILayout.Width(320f));
                GUILayout.Label($"{FormatUiInteger(inventoryItem.Units)} units", GUILayout.Width(90f));
                GUILayout.Label($"Unit {FormatCurrency(inventoryItem.UnitCost)} | Total {FormatCurrency(inventoryItem.TotalValue)}");
                GUILayout.EndHorizontal();
            }
        }
    }

    private void DrawFactoryTab()
    {
        DrawSection("Factory (M5)");
        if (_productionFactory is null)
        {
            DrawError("Production factory is not initialized.");
            return;
        }

        GUILayout.Label("Aircraft factory lines consume 21 MD per employee per month.");
        GUILayout.Label("Overflow MD carries over. Engine factory auto-scales to exact demand; excess capacity is discarded.");
        GUILayout.Label($"Factory Tool Level: {FormatUiInteger(_productionFactory.FactoryToolLevel)}");

        var targetFactoryToolLevel = DrawIntSlider("Target Factory Tool Level", _productionFactory.FactoryToolLevel, _productionFactory.FactoryToolLevel, 10);
        if (targetFactoryToolLevel > _productionFactory.FactoryToolLevel && GUILayout.Button("Upgrade Factory Tooling", GUILayout.Height(26f)))
        {
            _productionFactory.UpgradeFactoryToolLevel(targetFactoryToolLevel);
            _factoryActionMessage = $"Factory tooling upgraded to L{targetFactoryToolLevel}.";
        }

        if (GUILayout.Button("Add Production Line", GUILayout.Height(30f)))
        {
            var lineId = _productionFactory.AddLine();
            _factoryActionMessage = $"Added production line '{lineId}'.";
        }

        if (!string.IsNullOrWhiteSpace(_factoryActionMessage))
        {
            GUILayout.Label(_factoryActionMessage);
        }

        DrawSection("Warehouse");
        var liveWarehouseSnapshot = _productionFactory.WarehouseUnitsByDesign;
        DrawMetric("Stored Aircraft Units", liveWarehouseSnapshot.Values.Sum(), "0");
        if (liveWarehouseSnapshot.Count == 0)
        {
            GUILayout.Label("Warehouse is empty.");
        }
        else
        {
            foreach (var entry in liveWarehouseSnapshot.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal))
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{ResolveAircraftDesignName(entry.Key)} ({ResolveAircraftCategoryName(entry.Key)})", GUILayout.Width(360f));
                GUILayout.Label($"{FormatUiInteger(entry.Value)} units");
                GUILayout.EndHorizontal();
            }
        }

        var productionReadyDesigns = _savedAircraftDesigns
            .Where(IsAircraftDesignReadyForProduction)
            .ToArray();

        var blockedDesignsCount = _savedAircraftDesigns.Count - productionReadyDesigns.Length;
        if (blockedDesignsCount > 0)
        {
            GUILayout.Label($"{FormatUiInteger(blockedDesignsCount)} saved aircraft types are blocked until engine + family + type research is completed.");
        }

        var designOptions = new string[productionReadyDesigns.Length + 1];
        designOptions[0] = "(None)";
        for (var index = 0; index < productionReadyDesigns.Length; index++)
        {
            var design = productionReadyDesigns[index];
            var categoryName = AircraftCategoryCatalog.GetProfile(design.CatalogItem.Category).DisplayName;
            designOptions[index + 1] = $"{design.Name} | {categoryName} | Tool F{FormatUiInteger(design.CatalogItem.RequiredFactoryToolLevel)}/L{FormatUiInteger(design.CatalogItem.RequiredLineToolLevel)}";
        }

        if (productionReadyDesigns.Length == 0)
        {
            DrawError("No researched aircraft types available for production. Approve and complete research in Research tab.");
        }

        var linesToRemove = new List<string>();
        var lines = _productionFactory.Lines;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Line: {line.LineId}");

            var selectedDesignIndex = 0;
            if (!string.IsNullOrWhiteSpace(line.AssignedDesignId))
            {
                for (var index = 0; index < productionReadyDesigns.Length; index++)
                {
                    if (string.Equals(productionReadyDesigns[index].CatalogItem.DesignId, line.AssignedDesignId, StringComparison.Ordinal))
                    {
                        selectedDesignIndex = index + 1;
                        break;
                    }
                }
            }

            var nextSelectedDesignIndex = DrawListSelector($"Assigned Type ({line.LineId})", selectedDesignIndex, designOptions);
            var nextDesignId = nextSelectedDesignIndex == 0 ? null : productionReadyDesigns[nextSelectedDesignIndex - 1].CatalogItem.DesignId;
            if (!string.Equals(line.AssignedDesignId, nextDesignId, StringComparison.Ordinal))
            {
                _productionFactory.AssignDesign(line.LineId, nextDesignId);
            }

            var nextEmployees = DrawIntSlider($"Employees ({line.LineId})", line.Employees, 0, 500);
            if (nextEmployees != line.Employees)
            {
                _productionFactory.SetEmployees(line.LineId, nextEmployees);
            }

            var nextQueue = DrawIntSlider($"Queued Aircraft ({line.LineId})", line.QueuedUnits, 0, 5000);
            if (nextQueue != line.QueuedUnits)
            {
                _productionFactory.SetQueuedUnits(line.LineId, nextQueue);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 Queue", GUILayout.Height(24f)))
            {
                _productionFactory.AddQueuedUnits(line.LineId, 10);
            }

            if (GUILayout.Button("+100 Queue", GUILayout.Height(24f)))
            {
                _productionFactory.AddQueuedUnits(line.LineId, 100);
            }

            if (GUILayout.Button("Clear Queue", GUILayout.Height(24f)))
            {
                _productionFactory.SetQueuedUnits(line.LineId, 0);
            }

            GUILayout.EndHorizontal();

            var targetLineTool = DrawIntSlider($"Target Line Tool Level ({line.LineId})", line.LineToolLevel, line.LineToolLevel, 10);
            if (targetLineTool > line.LineToolLevel && GUILayout.Button($"Upgrade {line.LineId} Tooling", GUILayout.Height(24f)))
            {
                _productionFactory.UpgradeLineToolLevel(line.LineId, targetLineTool);
            }

            if (GUILayout.Button($"Remove {line.LineId}", GUILayout.Height(24f)))
            {
                linesToRemove.Add(line.LineId);
            }

            GUILayout.EndVertical();
        }

        for (var removeIndex = 0; removeIndex < linesToRemove.Count; removeIndex++)
        {
            _productionFactory.RemoveLine(linesToRemove[removeIndex]);
        }
    }

    private void DrawFactoryReport()
    {
        DrawSection("Factory Report");
        if (_productionFactory is null)
        {
            DrawError("Production factory is not initialized.");
            return;
        }

        if (_lastFactoryReport is null)
        {
            GUILayout.Label("No factory month processed yet. Press Next Month.");
            return;
        }

        GUILayout.Label($"Report Date: {FormatSimulationDate(_lastFactoryReport.Date)}");
        DrawMetric("Factory Tool Level", _lastFactoryReport.FactoryToolLevel, "0");
        DrawMetric("Produced Aircraft", _lastFactoryReport.TotalAircraftProduced, "0");
        DrawMetric("Queued Aircraft After Month", _lastFactoryReport.TotalQueuedUnitsAfterProduction, "0");
        DrawMetric("Warehouse Aircraft Units", _lastFactoryReport.TotalWarehouseUnits, "0");
        DrawMetric("Available MD", _lastFactoryReport.TotalAvailableManDays, "0.0");
        DrawMetric("Used MD", _lastFactoryReport.TotalUsedManDays, "0.0");
        DrawMetric("Capacity Utilization", _lastFactoryReport.CapacityUtilization * 100d, "0.0");

        DrawSection("Line Results");
        for (var index = 0; index < _lastFactoryReport.LineReports.Count; index++)
        {
            var line = _lastFactoryReport.LineReports[index];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{line.LineId} | {line.AssignedDesignName}");
            GUILayout.Label($"Status: {line.StatusMessage}");
            if (!string.IsNullOrWhiteSpace(line.AssignedDesignId))
            {
                GUILayout.Label($"Category: {ResolveAircraftCategoryName(line.AssignedDesignId)}");
            }

            GUILayout.Label($"Queue: {FormatUiInteger(line.StartQueueUnits)} -> {FormatUiInteger(line.EndQueueUnits)} | Produced: {FormatUiInteger(line.ProducedUnits)}");
            GUILayout.Label($"Staff: {FormatUiInteger(line.Employees)} | Tool L{FormatUiInteger(line.LineToolLevel)} (Req L{FormatUiInteger(line.RequiredLineToolLevel)})");
            GUILayout.Label($"MD: {FormatUiNumber(line.UsedManDays, "0.0")}/{FormatUiNumber(line.AvailableManDays, "0.0")} | Carry: {FormatUiNumber(line.CarryOverManDays, "0.0")}");
            GUILayout.Label($"Capacity: {FormatUiInteger(line.CapacityUnitsAtCurrentStaff)} units");
            GUILayout.Label($"Warehouse Units For Type: {FormatUiInteger(line.WarehouseUnitsAfterProduction)}");
            GUILayout.EndVertical();
        }

        DrawSection("Warehouse Snapshot");
        if (_lastFactoryReport.WarehouseUnitsByDesign.Count == 0)
        {
            GUILayout.Label("No aircraft in warehouse.");
        }
        else
        {
            foreach (var entry in _lastFactoryReport.WarehouseUnitsByDesign.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal))
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{ResolveAircraftDesignName(entry.Key)} ({ResolveAircraftCategoryName(entry.Key)})", GUILayout.Width(360f));
                GUILayout.Label($"{FormatUiInteger(entry.Value)} units");
                GUILayout.EndHorizontal();
            }
        }

        DrawSection("Engine Factory");
        var engineReport = _lastFactoryReport.EngineFactoryReport;
        DrawMetric("Required Engines", engineReport.TotalRequiredEngines, "0");
        DrawMetric("Produced Engines", engineReport.TotalProducedEngines, "0");
        DrawMetric("Discarded Engines", engineReport.TotalDiscardedEngines, "0");
        DrawMetric("Auto Employees", engineReport.TotalAutoAssignedEmployees, "0");

        for (var index = 0; index < engineReport.Entries.Count; index++)
        {
            var entry = engineReport.Entries[index];
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(entry.LineId, GUILayout.Width(100f));
            GUILayout.Label(entry.EngineDisplayName, GUILayout.Width(180f));
            GUILayout.Label($"Req {FormatUiInteger(entry.RequiredEngines)}, Discard {FormatUiInteger(entry.DiscardedEngines)}");
            GUILayout.EndHorizontal();
        }
    }

    private void SaveCurrentAircraftDesign()
    {
        RebuildAircraftPreview();
        if (_aircraftPreview is null)
        {
            _aircraftPreviewError = "Current aircraft values are invalid.";
            return;
        }

        if (_savedEngines.Count == 0)
        {
            _aircraftPreviewError = "Save at least one engine first.";
            return;
        }

        if (!TryGetSelectedCompatibleSavedEngine(out var selectedSavedEngine, out var engineSelectionError))
        {
            _aircraftPreviewError = engineSelectionError;
            return;
        }
        var name = string.IsNullOrWhiteSpace(_aircraftDesignName)
            ? $"{_aircraftPreview.FamilyDefinition.DisplayName} {_aircraftPreview.TypeDefinition.DisplayName}"
            : _aircraftDesignName.Trim();

        var baseId = $"{BuildFamilyId()}.{BuildTypeId()}";
        var uniqueId = CreateUniqueAircraftDesignId(baseId);
        var typeResearchId = $"type.{uniqueId}";

        if (_aircraftFactory is null)
        {
            _aircraftPreviewError = "Aircraft factory is not initialized.";
            return;
        }

        var typeModifiers = GetResearchModifierPoints(ResearchSubjectType.AircraftType, typeResearchId);
        var typeDefinition = BuildType(_aircraftPreview.FamilyDefinition.Id, typeModifiers);
        var aircraftForCatalog = _aircraftFactory.CreateAircraft(_aircraftPreview.FamilyDefinition, typeDefinition);

        var catalogItem = new FactoryAircraftTypeCatalogItem(
            uniqueId,
            name,
            aircraftForCatalog,
            aircraftForCatalog.FamilyDefinition.RequiredFactoryToolLevel,
            aircraftForCatalog.TypeDefinition.RequiredLineToolLevel);

        var savedDesign = new SavedAircraftDesign(
            name,
            catalogItem,
            selectedSavedEngine.ResearchId,
            aircraftForCatalog.FamilyDefinition.Id,
            typeResearchId);
        _savedAircraftDesigns.Add(savedDesign);
        _selectedSavedAircraftDesignIndex = _savedAircraftDesigns.Count - 1;

        RegisterFamilyResearchProposal(aircraftForCatalog.FamilyDefinition.Id, aircraftForCatalog.FamilyDefinition.DisplayName, aircraftForCatalog);
        RegisterTypeResearchProposal(typeResearchId, aircraftForCatalog.TypeDefinition.DisplayName, aircraftForCatalog);

        _factoryActionMessage =
            $"Saved aircraft design '{name}' ({AircraftCategoryCatalog.GetProfile(catalogItem.Category).DisplayName}) for production. " +
            "Type is blocked until engine/family/type research is approved and completed.";
    }

    private void SaveCurrentFamily()
    {
        var name = string.IsNullOrWhiteSpace(_familyName)
            ? $"Family {_savedFamilies.Count + 1}"
            : _familyName.Trim();
        var familyDisplayName = BuildFamilyDisplayName(name, _lockedEngineCount, _lockedMaterial, _lockedStructure, _familyAllowedEngineFamily);

        var familyId = BuildFamilyId();
        _savedFamilies.Add(new SavedFamilyDesign(
            name,
            familyId,
            _lockedEngineCount,
            _lockedStructure,
            _lockedMaterial,
            _baseAirframeParameters.WingPosition,
            _familyAllowedEngineFamily));
        _selectedSavedFamilyIndex = _savedFamilies.Count - 1;

        RebuildAircraftPreview();
        if (_aircraftPreview is not null)
        {
            RegisterFamilyResearchProposal(familyId, familyDisplayName, _aircraftPreview);
            _factoryActionMessage =
                $"Saved family '{familyDisplayName}'. Family research proposal added and awaits approval/completion.";
            return;
        }

        _factoryActionMessage = $"Saved family '{familyDisplayName}'.";
    }

    private void LoadSavedFamily(int index)
    {
        if (index < 0 || index >= _savedFamilies.Count)
        {
            return;
        }

        var saved = _savedFamilies[index];
        _familyName = saved.Name;
        _lockedEngineCount = saved.LockedEngineCount;
        _lockedStructure = saved.LockedStructureType;
        _lockedMaterial = saved.LockedMaterialType;
        _baseAirframeParameters.WingPosition = saved.WingPosition;
        _baseAirframeParameters.EngineCount = saved.LockedEngineCount;
        _baseAirframeParameters.StructureType = saved.LockedStructureType;
        _baseAirframeParameters.MaterialType = saved.LockedMaterialType;
        _familyAllowedEngineFamily = saved.AllowedEngineFamily;
        _selectedSavedFamilyIndex = index;
        _factoryActionMessage = $"Loaded family '{BuildFamilyDisplayName(saved.Name, saved.LockedEngineCount, saved.LockedMaterialType, saved.LockedStructureType, saved.AllowedEngineFamily)}'.";
    }

    private void ResetFamilyConfig()
    {
        _lockedEngineCount = NormalizeEngineCount(_defaultEngineCount);
        _lockedStructure = StructureType.Monocoque;
        _lockedMaterial = MaterialType.Aluminum;
        _familyAllowedEngineFamily = AircraftAllowedEngineFamily.Propeller;
        _baseAirframeParameters = CreateDefaultAirframeParameters(_lockedEngineCount);
    }

    private string CreateUniqueAircraftDesignId(string baseId)
    {
        var cleanBase = string.IsNullOrWhiteSpace(baseId) ? "aircraft.design" : baseId;
        var candidate = cleanBase;
        var suffix = 1;
        while (_savedAircraftDesigns.Any(design => string.Equals(design.CatalogItem.DesignId, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{cleanBase}.{suffix}";
            suffix++;
        }

        return candidate;
    }


    private string CreateUniqueEngineResearchId(string name)
    {
        var baseId = BuildNamedId("eng", name, "prototype");
        var candidate = baseId;
        var suffix = 1;
        while (_savedEngines.Any(engine => string.Equals(engine.ResearchId, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{baseId}.{suffix}";
            suffix++;
        }

        return candidate;
    }

    private void RegisterEngineResearchProposal(SavedEngineDesign savedDesign, Engine engineSnapshot)
    {
        if (_researchBoard is null)
        {
            return;
        }

        var engineDisplayName = BuildEngineDisplayName(savedDesign.Name, savedDesign.Definition.EngineType, savedDesign.Parameters);
        var points = GetResearchModifierPoints(ResearchSubjectType.Engine, savedDesign.ResearchId);
        var quote = _researchCalculator.CalculateEngineQuote(engineSnapshot, points);
        _researchBoard.UpsertProposal(
            ResearchSubjectType.Engine,
            savedDesign.ResearchId,
            engineDisplayName,
            quote);
        _researchActionMessage =
            $"Engine '{engineDisplayName}' submitted for approval: {FormatCurrency(quote.ResearchCost)} / {FormatUiInteger(quote.ResearchTimeMonths)} months.";
    }

    private void RegisterFamilyResearchProposal(string familyId, string familyName, Aircraft aircraft)
    {
        if (_researchBoard is null)
        {
            return;
        }

        var points = GetResearchModifierPoints(ResearchSubjectType.AircraftFamily, familyId);
        var quote = _researchCalculator.CalculateFamilyQuote(aircraft, points);
        _researchBoard.UpsertProposal(
            ResearchSubjectType.AircraftFamily,
            familyId,
            familyName,
            quote);
    }

    private void RegisterTypeResearchProposal(string typeId, string typeName, Aircraft aircraft)
    {
        if (_researchBoard is null)
        {
            return;
        }

        var points = GetResearchModifierPoints(ResearchSubjectType.AircraftType, typeId);
        var quote = _researchCalculator.CalculateTypeQuote(aircraft, points);
        _researchBoard.UpsertProposal(
            ResearchSubjectType.AircraftType,
            typeId,
            typeName,
            quote);
    }
    private IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> BuildProductionCatalog(bool includeOnlyResearched = false)
    {
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>(StringComparer.Ordinal);
        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var design = _savedAircraftDesigns[index];
            if (includeOnlyResearched && !IsAircraftDesignReadyForProduction(design))
            {
                continue;
            }

            catalog[design.CatalogItem.DesignId] = design.CatalogItem;
        }

        return catalog;
    }

    private bool IsAircraftDesignReadyForProduction(SavedAircraftDesign design)
    {
        if (design is null || _researchBoard is null)
        {
            return false;
        }

        return _researchBoard.IsCompleted(ResearchSubjectType.Engine, design.EngineResearchId)
            && _researchBoard.IsCompleted(ResearchSubjectType.AircraftFamily, design.FamilyResearchId)
            && _researchBoard.IsCompleted(ResearchSubjectType.AircraftType, design.TypeResearchId);
    }

    private string GetAircraftDesignResearchStatus(SavedAircraftDesign design)
    {
        if (_researchBoard is null)
        {
            return "Research board unavailable";
        }

        var missing = new List<string>();
        if (!_researchBoard.IsCompleted(ResearchSubjectType.Engine, design.EngineResearchId))
        {
            missing.Add("engine");
        }

        if (!_researchBoard.IsCompleted(ResearchSubjectType.AircraftFamily, design.FamilyResearchId))
        {
            missing.Add("family");
        }

        if (!_researchBoard.IsCompleted(ResearchSubjectType.AircraftType, design.TypeResearchId))
        {
            missing.Add("type");
        }

        return missing.Count == 0
            ? "Ready"
            : $"Pending: {string.Join(", ", missing)}";
    }

    private string ResolveAircraftDesignName(string designId)
    {
        if (string.IsNullOrWhiteSpace(designId))
        {
            return "(Unknown)";
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var design = _savedAircraftDesigns[index];
            if (string.Equals(design.CatalogItem.DesignId, designId, StringComparison.Ordinal))
            {
                return design.Name;
            }
        }

        return designId;
    }

    private string ResolveAircraftCategoryName(string designId)
    {
        if (string.IsNullOrWhiteSpace(designId))
        {
            return "Unknown Category";
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var design = _savedAircraftDesigns[index];
            if (string.Equals(design.CatalogItem.DesignId, designId, StringComparison.Ordinal))
            {
                return AircraftCategoryCatalog.GetProfile(design.CatalogItem.Category).DisplayName;
            }
        }

        return "Unknown Category";
    }

    private void PruneContractDeliverySelections(IReadOnlyList<Contract> openContracts)
    {
        var openIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < openContracts.Count; index++)
        {
            openIds.Add(openContracts[index].Id);
        }

        var staleKeys = _selectedWarehouseDesignByContractId.Keys
            .Where(contractId => !openIds.Contains(contractId))
            .ToArray();
        for (var index = 0; index < staleKeys.Length; index++)
        {
            _selectedWarehouseDesignByContractId.Remove(staleKeys[index]);
        }
    }

    private ContractAvailabilitySnapshot BuildContractAvailabilitySnapshot(
        Contract contract,
        ContractBoard contractBoard,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog,
        IReadOnlyDictionary<string, int> warehouseSnapshot)
    {
        var hasMatchingPlaneBySpecification = false;
        var canBeDeliveredNow = false;

        foreach (var catalogItem in catalog.Values)
        {
            var evaluation = contractBoard.Evaluate(contract, catalogItem.Aircraft);
            if (!evaluation.CanDeliver)
            {
                continue;
            }

            hasMatchingPlaneBySpecification = true;
            if (warehouseSnapshot.TryGetValue(catalogItem.DesignId, out var stockUnits)
                && stockUnits >= contract.QuantityUnits)
            {
                canBeDeliveredNow = true;
                break;
            }
        }

        return new ContractAvailabilitySnapshot(contract, canBeDeliveredNow, hasMatchingPlaneBySpecification);
    }

    private bool MatchesContractFilters(ContractAvailabilitySnapshot snapshot)
    {
        var minQuantity = System.Math.Min(_contractQuantityFilterMin, _contractQuantityFilterMax);
        var maxQuantity = System.Math.Max(_contractQuantityFilterMin, _contractQuantityFilterMax);
        var quantity = snapshot.Contract.QuantityUnits;
        if (quantity < minQuantity || quantity > maxQuantity)
        {
            return false;
        }

        if (_contractDeliverabilityFilter == ContractDeliverabilityFilter.CanBeDeliveredOnly
            && !snapshot.CanBeDeliveredNow)
        {
            return false;
        }

        return _contractSpecificationFilter switch
        {
            ContractSpecificationFilter.All => true,
            ContractSpecificationFilter.HaveMatchingPlane => snapshot.HasMatchingPlaneBySpecification,
            ContractSpecificationFilter.DoNotHaveMatchingPlane => !snapshot.HasMatchingPlaneBySpecification,
            _ => true,
        };
    }

    private void AdvanceToNextMonth()
    {
        _currentDate = _currentDate.NextMonth();
        _elapsedMonths++;

        if (_researchBoard is not null)
        {
            var completedThisMonth = _researchBoard.AdvanceMonth(_elapsedMonths);
            if (completedThisMonth > 0)
            {
                _researchActionMessage =
                    $"{FormatUiInteger(completedThisMonth)} research project(s) completed in {FormatSimulationDate(_currentDate)}.";
            }
        }

        if (_contractBoard is not null)
        {
            var canceledBefore = _contractBoard.CanceledContracts.Count;
            var generated = _contractBoard.AdvanceMonthAndGenerateContracts();
            _lastGeneratedContracts.Clear();
            _lastGeneratedContracts.AddRange(generated);
            var canceledDelta = _contractBoard.CanceledContracts.Count - canceledBefore;
            _contractActionMessage = $"Advanced to {FormatSimulationDate(_currentDate)}. Generated {FormatUiInteger(generated.Count)} contracts, canceled {FormatUiInteger(canceledDelta)}.";
        }

        if (_governmentContractBoard is not null)
        {
            var governmentUpdate = _governmentContractBoard.ProcessMonth(_currentDate);
            if (governmentUpdate.Events.Count > 0)
            {
                _governmentContractActionMessage = string.Join(" ", governmentUpdate.Events);
            }

            if (governmentUpdate.EvaluationResult is not null || governmentUpdate.ExpiredContract is not null)
            {
                CloseGovernmentContractNotebookWindow();
            }
            else if (_governmentContractNotebookWindow is not null)
            {
                RefreshGovernmentContractNotebookWindowContent();
            }
        }

        if (_productionFactory is not null)
        {
            var productionCatalog = BuildProductionCatalog(includeOnlyResearched: true);
            var fullCatalog = BuildProductionCatalog(includeOnlyResearched: false);
            _lastFactoryReport = _productionFactory.SimulateMonth(productionCatalog, _elapsedMonths, _currentDate);
            _factoryActionMessage =
                $"Factory processed month {FormatUiInteger(_elapsedMonths)}: produced {FormatUiInteger(_lastFactoryReport.TotalAircraftProduced)} aircraft.";

            if (_accountingSystem is not null)
            {
                var researchExpenses = _researchBoard?.GetBookedResearchExpenseForMonth(_elapsedMonths) ?? 0d;
                var monthlyCosts = _factoryAccountingCalculator.CalculateMonthlyCosts(_lastFactoryReport, productionCatalog)
                    with
                    {
                        VariableResearchExpenses = researchExpenses,
                    };
                var inventoryByDesign = _factoryAccountingCalculator.CalculateInventoryValueByDesign(
                    _lastFactoryReport.WarehouseUnitsByDesign,
                    fullCatalog);
                _accountingSystem.RegisterMonthlyFactoryCosts(
                    _elapsedMonths,
                    _currentDate,
                    monthlyCosts,
                    inventoryByDesign);
            }

            if (_governmentContractBoard is not null && _governmentInventoryGateway is not null)
            {
                var deliveryContractBefore = _governmentContractBoard.ActiveDeliveryContract;
                var deliveryUpdate = _governmentContractBoard.ProcessDeliveryFromInventory(_currentDate, _governmentInventoryGateway);

                var bookedIncome = 0d;
                if (deliveryContractBefore is not null && deliveryUpdate.DeliveredUnitsThisMonth > 0)
                {
                    bookedIncome += deliveryUpdate.DeliveredUnitsThisMonth * deliveryContractBefore.UnitPrice;
                }

                if (deliveryUpdate.CompletionBonusGranted > 0d)
                {
                    bookedIncome += deliveryUpdate.CompletionBonusGranted;
                }

                if (_accountingSystem is not null && bookedIncome > 0d)
                {
                    var inventoryByDesign = _factoryAccountingCalculator.CalculateInventoryValueByDesign(
                        _productionFactory.WarehouseUnitsByDesign,
                        fullCatalog);
                    _accountingSystem.RegisterContractIncome(
                        _elapsedMonths,
                        _currentDate,
                        bookedIncome,
                        inventoryByDesign);
                    _governmentDeliveryActionMessage = $"{deliveryUpdate.Message} Revenue booked: {FormatCurrency(bookedIncome)}.";
                }
                else if (deliveryContractBefore is not null
                    || deliveryUpdate.CompletedThisMonth
                    || deliveryUpdate.EndedThisMonth)
                {
                    _governmentDeliveryActionMessage = deliveryUpdate.Message;
                }
            }
        }

        MaybeCreateAutosave();
    }

    private void RebuildEnginePreview()
    {
        _enginePreview = null;
        _enginePreviewError = string.Empty;
        if (_engineBuilder is null || _engineDefinitions.Count == 0)
        {
            return;
        }

        try
        {
            _enginePreview = _engineBuilder.Build(_engineDefinitions[_selectedEngineDefinitionIndex], _engineParameters);
        }
        catch (Exception ex)
        {
            _enginePreviewError = ex.Message;
        }
    }

    private void RebuildAircraftPreview()
    {
        _aircraftPreview = null;
        _aircraftPreviewError = string.Empty;

        if (_savedEngines.Count == 0)
        {
            _aircraftPreviewError = "Save at least one engine first.";
            return;
        }

        if (_aircraftFactory is null)
        {
            _aircraftPreviewError = "Aircraft factory is not initialized.";
            return;
        }

        try
        {
            if (!TryGetSelectedCompatibleSavedEngine(out var saved, out var engineSelectionError))
            {
                _aircraftPreviewError = engineSelectionError;
                return;
            }

            var family = BuildFamily(saved);
            var type = BuildType(family.Id);
            _aircraftPreview = _aircraftFactory.CreateAircraft(family, type);
        }
        catch (Exception ex)
        {
            _aircraftPreviewError = ex.Message;
        }
    }

    private void SaveCurrentEngine()
    {
        if (_enginePreview is null)
        {
            _enginePreviewError = "Current engine values are invalid.";
            return;
        }

        var name = string.IsNullOrWhiteSpace(_engineDesignName)
            ? $"Engine-{_savedEngines.Count + 1}"
            : _engineDesignName.Trim();
        var researchId = CreateUniqueEngineResearchId(name);
        var savedDesign = new SavedEngineDesign(name, researchId, _enginePreview);
        _savedEngines.Add(savedDesign);
        _selectedSavedEngineIndex = _savedEngines.Count - 1;

        RegisterEngineResearchProposal(savedDesign, _enginePreview);
    }

    private void LoadSavedEngine(int index)
    {
        if (index < 0 || index >= _savedEngines.Count)
        {
            return;
        }

        var saved = _savedEngines[index];
        _engineDesignName = saved.Name;
        _engineParameters = saved.Parameters.Clone();
        _selectedEngineDefinitionIndex = FindIndexById(_engineDefinitions.Select(d => d.Id).ToArray(), saved.Definition.Id);
        SyncEngineSelectorsFromDefinition(saved.Definition.EngineType);
        _selectedSavedEngineIndex = index;
        RebuildEnginePreview();
    }

    private IReadOnlyList<int> GetCompatibleSavedEngineIndices()
    {
        var compatibleIndices = new List<int>();
        for (var index = 0; index < _savedEngines.Count; index++)
        {
            var engineType = _savedEngines[index].Definition.EngineType;
            if (AircraftEngineFamilyCompatibility.IsEngineTypeAllowed(_familyAllowedEngineFamily, engineType))
            {
                compatibleIndices.Add(index);
            }
        }

        return compatibleIndices;
    }

    private int ResolveSelectedCompatibleEngineOptionIndex(IReadOnlyList<int> compatibleEngineIndices)
    {
        if (compatibleEngineIndices is null || compatibleEngineIndices.Count == 0)
        {
            return 0;
        }

        for (var index = 0; index < compatibleEngineIndices.Count; index++)
        {
            if (compatibleEngineIndices[index] == _selectedSavedEngineIndex)
            {
                return index;
            }
        }

        _selectedSavedEngineIndex = compatibleEngineIndices[0];
        return 0;
    }

    private bool TryGetSelectedCompatibleSavedEngine(out SavedEngineDesign saved, out string errorMessage)
    {
        var compatibleEngineIndices = GetCompatibleSavedEngineIndices();
        if (compatibleEngineIndices.Count == 0)
        {
            saved = null!;
            errorMessage = $"No saved {GetAllowedEngineFamilyLabel(_familyAllowedEngineFamily).ToLowerInvariant()} engines available for this family.";
            return false;
        }

        var selectedIndex = ResolveSelectedCompatibleEngineOptionIndex(compatibleEngineIndices);
        _selectedSavedEngineIndex = compatibleEngineIndices[selectedIndex];
        saved = _savedEngines[_selectedSavedEngineIndex];
        errorMessage = string.Empty;
        return true;
    }

    private AircraftFamilyDefinition BuildFamily(SavedEngineDesign saved)
    {
        if (!AircraftEngineFamilyCompatibility.IsEngineTypeAllowed(_familyAllowedEngineFamily, saved.Definition.EngineType))
        {
            throw new InvalidOperationException(
                $"Selected engine '{saved.Definition.DisplayName}' is not allowed for a {GetAllowedEngineFamilyLabel(_familyAllowedEngineFamily).ToLowerInvariant()} family.");
        }

        var baseAirframe = _baseAirframeParameters.Clone();
        baseAirframe.EngineCount = _lockedEngineCount;
        baseAirframe.StructureType = _lockedStructure;
        baseAirframe.MaterialType = _lockedMaterial;

        return new AircraftFamilyDefinition
        {
            Id = BuildFamilyId(),
            DisplayName = BuildFamilyDisplayName(_familyName, _lockedEngineCount, _lockedMaterial, _lockedStructure, _familyAllowedEngineFamily),
            EngineDefinitionId = saved.Definition.Id,
            AirframeDefinitionId = _airframeDefinitions[_selectedAirframeDefinitionIndex].Id,
            LockedEngineType = saved.Definition.EngineType,
            AllowedEngineFamily = _familyAllowedEngineFamily,
            LockedStructureType = _lockedStructure,
            LockedMaterialType = _lockedMaterial,
            LockedEngineCount = _lockedEngineCount,
            Category = _selectedAircraftCategory,
            RequiredFactoryToolLevel = FixedFactoryToolRequirementLevel,
            BaseDevelopmentCost = _baseDevelopmentCost,
            BaseDevelopmentManDays = _baseDevelopmentManDays,
            BaseEngineParameters = saved.Parameters.Clone(),
            BaseAirframeParameters = baseAirframe,
        };
    }

    private AircraftTypeDefinition BuildType(string familyId, TypeResearchModifierPoints? researchModifiers = null)
    {
        return new AircraftTypeDefinition
        {
            Id = BuildTypeId(),
            FamilyId = familyId,
            DisplayName = BuildTypeDisplayName(),
            RequiredLineToolLevel = 1,
            EngineDelta = new EngineParameterDelta(),
            AirframeDelta = BuildAirframeDeltaFromType(),
            ResearchModifiers = (researchModifiers ?? new TypeResearchModifierPoints()).Clone(),
        };
    }

    private AirframeParameterDelta BuildAirframeDeltaFromType()
    {
        var wingAreaDelta = _typeWingAreaSquareMeters - _baseAirframeParameters.WingAreaSquareMeters;
        var fuelDelta = _typeFuelLiters - _baseAirframeParameters.FuelAmountLiters;
        var cowlingDelta = _typeCowling - _baseAirframeParameters.EngineCowling;
        var aerodynamicCleanlinessDelta = _typeAerodynamicCleanlinessPercent - _baseAirframeParameters.AerodynamicCleanlinessPercent;
        var crewDelta = _typeCrewCount - _baseAirframeParameters.CrewCount;

        return new AirframeParameterDelta
        {
            WingAreaSquareMetersDelta = wingAreaDelta,
            FuelAmountLitersDelta = fuelDelta,
            CrewCountDelta = crewDelta,
            EngineCowlingDelta = cowlingDelta,
            AerodynamicCleanlinessPercentDelta = aerodynamicCleanlinessDelta,
            WingShapeOverride = _typeWingShape,
            WingProfileOverride = _typeWingProfile,
            TailTypeOverride = _typeTailType,
            FlapTypeOverride = _typeFlapType,
            HasLeadingEdgeFlapsOverride = _typeHasLeadingEdgeFlaps,
            HasBackupSystemsOverride = _typeHasBackupSystems,
            LandingGearTypeOverride = _typeLandingGearType,
            RunwayTypeOverride = _typeRunwayType,
        };
    }
    private (string EngineJsonPath, string AirframeJsonPath) ResolveDefinitionPaths()
    {
        var streamingPath = Path.Combine(Application.streamingAssetsPath, "Data", "Examples");
        var streamingEngine = Path.Combine(streamingPath, "engine-definitions.json");
        var streamingAirframe = Path.Combine(streamingPath, "airframe-definitions.json");
        if (File.Exists(streamingEngine) && File.Exists(streamingAirframe))
        {
            return (streamingEngine, streamingAirframe);
        }

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var rootEngine = Path.Combine(projectRoot, "Data", "Examples", "engine-definitions.json");
        var rootAirframe = Path.Combine(projectRoot, "Data", "Examples", "airframe-definitions.json");
        if (File.Exists(rootEngine) && File.Exists(rootAirframe))
        {
            return (rootEngine, rootAirframe);
        }

        throw new FileNotFoundException("Definition JSON files were not found in StreamingAssets or Data/Examples.");
    }

    private static void DrawSection(string title)
    {
        GUILayout.Space(6f);
        GUILayout.Label(title, GUI.skin.box);
    }

    private static readonly CultureInfo UiNumberCulture = CreateUiNumberCulture();

    private static CultureInfo CreateUiNumberCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSizes = new[] { 3 };
        return culture;
    }

    private static string FormatUiNumber(double value, string format)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString(BuildGroupedNumericFormat(format), UiNumberCulture);
    }

    private static string FormatUiInteger(int value) => value.ToString("#,##0", UiNumberCulture);

    private static string BuildGroupedNumericFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "#,##0.###";
        }

        if (format.StartsWith("N", StringComparison.OrdinalIgnoreCase) || format.Contains(','))
        {
            return format;
        }

        return $"#,##{format}";
    }

    private static void DrawMetric(string label, double value, string format)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(240f));
        GUILayout.Label(FormatUiNumber(value, format));
        GUILayout.EndHorizontal();
    }

    private static void DrawError(string message)
    {
        var previous = GUI.color;
        GUI.color = new Color(1f, 0.45f, 0.45f, 1f);
        GUILayout.Label(message);
        GUI.color = previous;
    }

    private double GetContractUnitPrice(Contract contract)
    {
        if (contract.UnitPrice > 0d)
        {
            return contract.UnitPrice;
        }

        return _contractPricingCalculator.CalculateQuote(contract.Requirements, contract.QuantityUnits).UnitPrice;
    }

    private double GetContractTotalPrice(Contract contract)
    {
        if (contract.TotalPrice > 0d)
        {
            return contract.TotalPrice;
        }

        return _contractPricingCalculator.CalculateQuote(contract.Requirements, contract.QuantityUnits).TotalPrice;
    }

    private double CalculateContractProfitLoss(Contract contract, FactoryAircraftTypeCatalogItem selectedAircraftDesign)
    {
        var revenue = GetContractTotalPrice(contract);
        var unitFulfillmentCost = CalculateAircraftUnitCostWithManDayLabor(selectedAircraftDesign.Aircraft);
        var fulfillmentCost = unitFulfillmentCost * contract.QuantityUnits;
        return revenue - fulfillmentCost;
    }

    private static double CalculateAircraftUnitCostWithManDayLabor(Aircraft aircraft)
    {
        var engineCount = aircraft.Airframe.Parameters.EngineCount;
        var unitHardwareCost = aircraft.Airframe.Performance.ProductionCost + (aircraft.Engine.Performance.Cost * engineCount);

        var airframeManDays = aircraft.Airframe.Performance.ProductionManDays;
        var engineManDays = aircraft.Engine.Performance.ProductionManDays * engineCount;

        var planeLaborCostPerManDay = FactoryBalance.PlaneStaffMonthlyCostPerEmployee
            / System.Math.Max(FactoryBalance.EmployeeManDaysPerMonth, 1d);
        var engineLaborCostPerManDay = FactoryBalance.EngineStaffMonthlyCostPerEmployee
            / System.Math.Max(FactoryBalance.EmployeeManDaysPerMonth, 1d);

        var unitLaborCost = (airframeManDays * planeLaborCostPerManDay)
            + (engineManDays * engineLaborCostPerManDay);

        return unitHardwareCost + unitLaborCost;
    }

    private static void DrawSignedCurrencyMetric(string label, double value)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(240f));
        DrawSignedCurrencyInline(FormatCurrency(value), value, 180f);
        GUILayout.EndHorizontal();
    }

    private static void DrawSignedCurrencyInline(string text, double value, float width)
    {
        var previous = GUI.color;
        GUI.color = value >= 0d
            ? new Color(0.45f, 1f, 0.55f, 1f)
            : new Color(1f, 0.45f, 0.45f, 1f);
        GUILayout.Label(text, GUILayout.Width(width));
        GUI.color = previous;
    }

    private static void DrawHorizontalBar(double value, double maxValue, Color color)
    {
        var rect = GUILayoutUtility.GetRect(140f, 16f, GUILayout.Width(140f), GUILayout.Height(16f));
        DrawRect(rect, new Color(0.18f, 0.22f, 0.3f, 1f));

        var safeMax = System.Math.Max(maxValue, 1d);
        var ratio = Mathf.Clamp01((float)(value / safeMax));
        if (ratio <= 0f)
        {
            return;
        }

        var fillRect = new Rect(rect.x, rect.y, rect.width * ratio, rect.height);
        DrawRect(fillRect, color);
    }

    private static void DrawIncomeExpenseTimelineChart(IReadOnlyList<MonthlyAccountingSnapshot> history)
    {
        if (history.Count == 0)
        {
            GUILayout.Label("No monthly accounting history yet.");
            return;
        }

        var snapshots = history.Count <= 12
            ? history.ToArray()
            : history.Skip(System.Math.Max(0, history.Count - 12)).ToArray();
        var chartRect = GUILayoutUtility.GetRect(760f, 390f, GUILayout.ExpandWidth(true));

        DrawRect(chartRect, new Color(0.92f, 0.92f, 0.92f, 1f));

        var plotRect = new Rect(
            chartRect.x + 56f,
            chartRect.y + 42f,
            chartRect.width - 76f,
            chartRect.height - 86f);

        var maxAbsValue = snapshots
            .Select(snapshot => System.Math.Max(
                System.Math.Max(snapshot.Income, snapshot.Costs.TotalCosts),
                System.Math.Abs(snapshot.MonthlyProfitLoss)))
            .DefaultIfEmpty(1d)
            .Max();
        var scaleStep = RoundUpToNiceStep(maxAbsValue / 3d);
        var scaleMax = System.Math.Max(scaleStep * 3d, 1d);

        float MapY(double value)
        {
            var normalized = (value + scaleMax) / (scaleMax * 2d);
            return (float)(plotRect.yMax - (normalized * plotRect.height));
        }

        var axisStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
        };
        axisStyle.normal.textColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        var monthStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold,
        };
        monthStyle.normal.textColor = new Color(0.36f, 0.36f, 0.36f, 1f);

        var yearStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
        };
        yearStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        var ticks = new[] { scaleMax, scaleMax * 0.5d, 0d, -scaleMax * 0.5d, -scaleMax };
        for (var tickIndex = 0; tickIndex < ticks.Length; tickIndex++)
        {
            var tickValue = ticks[tickIndex];
            var y = MapY(tickValue);
            var isZeroLine = System.Math.Abs(tickValue) <= 0.001d;
            var gridColor = isZeroLine
                ? new Color(0.06f, 0.06f, 0.06f, 1f)
                : new Color(0.8f, 0.8f, 0.8f, 1f);
            var thickness = isZeroLine ? 2f : 1f;
            DrawRect(new Rect(plotRect.x, y - (thickness * 0.5f), plotRect.width, thickness), gridColor);
            GUI.Label(new Rect(chartRect.x + 3f, y - 10f, 46f, 20f), FormatAxisThousands(tickValue), axisStyle);
        }

        var firstYear = snapshots[0].Date.Year;
        var lastYear = snapshots[snapshots.Length - 1].Date.Year;
        var yearText = firstYear == lastYear
            ? firstYear.ToString(CultureInfo.InvariantCulture)
            : $"{firstYear}-{lastYear}";
        GUI.Label(new Rect(plotRect.x + 2f, chartRect.y + 10f, 120f, 24f), yearText, yearStyle);

        var incomeColor = new Color(0.66f, 0.82f, 0.55f, 1f);
        var expenseColor = new Color(0.9f, 0.28f, 0.28f, 1f);
        var profitGainLineColor = new Color(0.22f, 0.55f, 0.22f, 0.95f);
        var profitLossLineColor = new Color(0.85f, 0.2f, 0.2f, 0.95f);

        var legendStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
        };
        legendStyle.normal.textColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        var legendY = chartRect.y + 12f;
        var incomeLegendRect = new Rect(chartRect.xMax - 320f, legendY + 2f, 12f, 12f);
        var expenseLegendRect = new Rect(chartRect.xMax - 230f, legendY + 2f, 12f, 12f);
        DrawRect(incomeLegendRect, incomeColor);
        DrawRect(expenseLegendRect, expenseColor);
        GUI.Label(new Rect(incomeLegendRect.x + 16f, legendY, 70f, 20f), "Income", legendStyle);
        GUI.Label(new Rect(expenseLegendRect.x + 16f, legendY, 80f, 20f), "Expense", legendStyle);

        DrawRect(new Rect(chartRect.xMax - 130f, legendY + 7f, 24f, 2f), profitGainLineColor);
        DrawRect(new Rect(chartRect.xMax - 130f, legendY + 13f, 24f, 2f), profitLossLineColor);
        GUI.Label(new Rect(chartRect.xMax - 100f, legendY, 95f, 20f), "P/L Line", legendStyle);

        var slotWidth = plotRect.width / snapshots.Length;
        var groupedBarWidth = Mathf.Clamp(slotWidth * 0.78f, 18f, 42f);
        var barGap = Mathf.Clamp(groupedBarWidth * 0.14f, 2f, 6f);
        var singleBarWidth = Mathf.Clamp((groupedBarWidth - barGap) * 0.5f, 7f, 20f);
        var profitLossLineThickness = Mathf.Clamp(slotWidth * 0.0375f, 1.5f, 3.75f);
        var zeroY = MapY(0d);
        var profitLossPoints = new Vector2[snapshots.Length];
        var profitLossValues = new double[snapshots.Length];

        for (var index = 0; index < snapshots.Length; index++)
        {
            var snapshot = snapshots[index];
            var centerX = plotRect.x + (slotWidth * (index + 0.5f));
            var incomeValue = snapshot.Income;
            var expenseValue = snapshot.Costs.TotalCosts;

            var incomeY = MapY(incomeValue);
            var expenseY = MapY(-expenseValue);

            var groupLeft = centerX - (groupedBarWidth * 0.5f);
            var incomeLeft = groupLeft;
            var expenseLeft = groupLeft + singleBarWidth + barGap;

            if (incomeValue > 0d)
            {
                DrawRect(new Rect(incomeLeft, incomeY, singleBarWidth, zeroY - incomeY), incomeColor);
            }

            if (expenseValue > 0d)
            {
                DrawRect(new Rect(expenseLeft, zeroY, singleBarWidth, expenseY - zeroY), expenseColor);
            }

            var profitLossValue = snapshot.MonthlyProfitLoss;
            profitLossValues[index] = profitLossValue;
            profitLossPoints[index] = new Vector2(centerX, MapY(profitLossValue));

            GUI.Label(
                new Rect(centerX - (slotWidth * 0.5f), plotRect.yMax + 7f, slotWidth, 24f),
                ToMonthShortLabel(snapshot.Date.Month),
                monthStyle);
        }

        for (var index = 1; index < profitLossPoints.Length; index++)
        {
            var previousValue = profitLossValues[index - 1];
            var currentValue = profitLossValues[index];
            var previousPoint = profitLossPoints[index - 1];
            var currentPoint = profitLossPoints[index];

            var previousIsLoss = previousValue < 0d;
            var currentIsLoss = currentValue < 0d;
            if (previousIsLoss == currentIsLoss)
            {
                var color = currentIsLoss ? profitLossLineColor : profitGainLineColor;
                DrawLine(previousPoint, currentPoint, color, profitLossLineThickness);
                continue;
            }

            var denominator = currentValue - previousValue;
            var blend = System.Math.Abs(denominator) <= 1e-9d
                ? 0.5f
                : Mathf.Clamp01((float)(-previousValue / denominator));
            var zeroCrossing = Vector2.Lerp(previousPoint, currentPoint, blend);

            DrawLine(
                previousPoint,
                zeroCrossing,
                previousIsLoss ? profitLossLineColor : profitGainLineColor,
                profitLossLineThickness);
            DrawLine(
                zeroCrossing,
                currentPoint,
                currentIsLoss ? profitLossLineColor : profitGainLineColor,
                profitLossLineThickness);
        }
    }

    private static string ToMonthShortLabel(int month)
    {
        return month switch
        {
            1 => "JAN",
            2 => "FEB",
            3 => "MAR",
            4 => "APR",
            5 => "MAY",
            6 => "JUN",
            7 => "JUL",
            8 => "AUG",
            9 => "SEP",
            10 => "OCT",
            11 => "NOV",
            12 => "DEC",
            _ => month.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static string FormatAxisThousands(double value)
    {
        var absolute = System.Math.Abs(value);
        if (absolute >= 1000d)
        {
            return $"{FormatUiNumber(value / 1000d, "0.#")}K";
        }

        return FormatUiNumber(value, "0");
    }

    private static double RoundUpToNiceStep(double value)
    {
        if (value <= 0d)
        {
            return 1d;
        }

        var exponent = System.Math.Pow(10d, System.Math.Floor(System.Math.Log10(value)));
        var normalized = value / exponent;
        var nice = normalized switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d,
        };

        return nice * exponent;
    }

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var previousColor = GUI.color;
        var previousMatrix = GUI.matrix;
        var angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
        var length = Vector2.Distance(start, end);

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - (thickness * 0.5f), length, thickness), Texture2D.whiteTexture);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private static string FormatCurrency(double value)
    {
        var rounded = System.Math.Round(value, 0, MidpointRounding.AwayFromZero);
        return "$" + FormatUiNumber(rounded, "0");
    }

    private int DrawListSelector(string label, int selectedIndex, string[] options)
    {
        GUILayout.Label(label);
        if (options.Length == 0)
        {
            return 0;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Length - 1);
        selectedIndex = GUILayout.SelectionGrid(selectedIndex, options, 1);
        return Mathf.Clamp(selectedIndex, 0, options.Length - 1);
    }

    private int DrawEngineCountSelector(string label, int currentValue)
    {
        GUILayout.Label(label);
        var labels = _engineCountOptions.Select(c => c.ToString()).ToArray();
        var index = Array.IndexOf(_engineCountOptions, currentValue);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, labels.Length);
        next = Mathf.Clamp(next, 0, _engineCountOptions.Length - 1);
        return _engineCountOptions[next];
    }

    private static string DrawText(string label, string value)
    {
        GUILayout.Label(label);
        return GUILayout.TextField(value ?? string.Empty);
    }

    private double DrawSlider(string label, double value, double min, double max, string format)
    {
        var currentValue = Math.Clamp(value, min, max);
        var key = BuildNumericFieldKey("double", label, min, max);
        var controlName = BuildControlName("double", key);
        _doubleSliderHadFocus.TryGetValue(key, out var hadFocus);

        if (!_doubleSliderInputBuffer.TryGetValue(key, out var currentInput))
        {
            currentInput = FormatUiNumber(currentValue, format);
            _doubleSliderInputBuffer[key] = currentInput;
        }

        GUILayout.Label($"{label}: {FormatUiNumber(currentValue, format)}");
        GUILayout.BeginHorizontal();
        var sliderValue = GUILayout.HorizontalSlider((float)currentValue, (float)min, (float)max, GUILayout.ExpandWidth(true));
        GUI.SetNextControlName(controlName);
        var nextInput = GUILayout.TextField(currentInput, GUILayout.Width(120f));
        GUILayout.Label(
            $"[{FormatUiNumber(min, format)} - {FormatUiNumber(max, format)}]",
            GUILayout.Width(185f));
        GUILayout.EndHorizontal();

        var hasFocus = GUI.GetNameOfFocusedControl() == controlName;
        var pressedEnter = hasFocus
            && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        var lostFocus = hadFocus && !hasFocus;

        var sliderRounded = Math.Clamp(Math.Round(sliderValue, 4, MidpointRounding.AwayFromZero), min, max);
        var sliderChanged = Math.Abs(sliderRounded - currentValue) > 1e-6;
        if (sliderChanged)
        {
            currentValue = sliderRounded;
            _doubleSliderInputBuffer[key] = FormatUiNumber(currentValue, format);
            _doubleSliderHadFocus[key] = hasFocus;
            return currentValue;
        }

        _doubleSliderInputBuffer[key] = nextInput;

        if (pressedEnter || lostFocus)
        {
            if (TryParseDecimalCandidate(nextInput, out var parsedValue)
                && parsedValue >= min
                && parsedValue <= max)
            {
                currentValue = Math.Round(parsedValue, 4, MidpointRounding.AwayFromZero);
                _doubleSliderInputBuffer[key] = FormatUiNumber(currentValue, format);
            }

            if (pressedEnter)
            {
                Event.current.Use();
            }
        }
        else if (!hasFocus)
        {
            _doubleSliderInputBuffer[key] = FormatUiNumber(currentValue, format);
        }

        _doubleSliderHadFocus[key] = hasFocus;
        return currentValue;
    }

    private int DrawIntSlider(string label, int value, int min, int max)
    {
        var currentValue = Math.Clamp(value, min, max);
        var key = BuildNumericFieldKey("int", label, min, max);
        var controlName = BuildControlName("int", key);
        _intSliderHadFocus.TryGetValue(key, out var hadFocus);

        if (!_intSliderInputBuffer.TryGetValue(key, out var currentInput))
        {
            currentInput = FormatUiInteger(currentValue);
            _intSliderInputBuffer[key] = currentInput;
        }

        GUILayout.Label($"{label}: {FormatUiInteger(currentValue)}");
        GUILayout.BeginHorizontal();
        var sliderValue = GUILayout.HorizontalSlider(currentValue, min, max, GUILayout.ExpandWidth(true));
        GUI.SetNextControlName(controlName);
        var nextInput = GUILayout.TextField(currentInput, GUILayout.Width(120f));
        GUILayout.Label($"[{FormatUiInteger(min)} - {FormatUiInteger(max)}]", GUILayout.Width(185f));
        GUILayout.EndHorizontal();

        var hasFocus = GUI.GetNameOfFocusedControl() == controlName;
        var pressedEnter = hasFocus
            && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        var lostFocus = hadFocus && !hasFocus;

        var sliderRounded = Mathf.Clamp(Mathf.RoundToInt(sliderValue), min, max);
        var sliderChanged = sliderRounded != currentValue;
        if (sliderChanged)
        {
            currentValue = sliderRounded;
            _intSliderInputBuffer[key] = FormatUiInteger(currentValue);
            _intSliderHadFocus[key] = hasFocus;
            return currentValue;
        }

        _intSliderInputBuffer[key] = nextInput;

        if (pressedEnter || lostFocus)
        {
            if (TryParseIntegerCandidate(nextInput, out var parsedValue)
                && parsedValue >= min
                && parsedValue <= max)
            {
                currentValue = parsedValue;
                _intSliderInputBuffer[key] = FormatUiInteger(currentValue);
            }

            if (pressedEnter)
            {
                Event.current.Use();
            }
        }
        else if (!hasFocus)
        {
            _intSliderInputBuffer[key] = FormatUiInteger(currentValue);
        }

        _intSliderHadFocus[key] = hasFocus;
        return currentValue;
    }

    private static string BuildNumericFieldKey(string family, string label, double min, double max) =>
        $"{family}:{label}:{min.ToString("0.####", CultureInfo.InvariantCulture)}:{max.ToString("0.####", CultureInfo.InvariantCulture)}";

    private static string BuildNumericFieldKey(string family, string label, int min, int max) =>
        $"{family}:{label}:{min}:{max}";

    private static string BuildControlName(string prefix, string key) =>
        $"{prefix}-input-{key.GetHashCode().ToString(CultureInfo.InvariantCulture)}";

    private static bool TryParseDecimalCandidate(string rawInput, out double value)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            value = 0d;
            return false;
        }

        var normalized = rawInput.Trim().Replace(" ", string.Empty).Replace("\u00A0", string.Empty).Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseIntegerCandidate(string rawInput, out int value)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            value = 0;
            return false;
        }

        var normalized = rawInput.Trim().Replace(" ", string.Empty).Replace("\u00A0", string.Empty).Replace(",", string.Empty);
        return int.TryParse(
            normalized,
            NumberStyles.Integer | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static TEnum DrawEnum<TEnum>(string label, TEnum current)
        where TEnum : struct, Enum
    {
        GUILayout.Label(label);
        var values = (TEnum[])Enum.GetValues(typeof(TEnum));
        var labels = values.Select(v => v.ToString()).ToArray();
        var index = Array.IndexOf(values, current);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, Math.Min(3, labels.Length));
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static void DrawOptionalEnum<TEnum>(string label, ref bool enabled, ref TEnum value)
        where TEnum : struct, Enum
    {
        GUILayout.BeginVertical(GUI.skin.box);
        enabled = GUILayout.Toggle(enabled, $"{label} Enabled");
        if (enabled)
        {
            value = DrawEnum(label, value);
        }
        else
        {
            GUILayout.Label($"{label}: None");
        }

        GUILayout.EndVertical();
    }

    private static ContractDeliverabilityFilter DrawContractDeliverabilityFilter(string label, ContractDeliverabilityFilter currentFilter)
    {
        GUILayout.Label(label);
        var labels = new[] { "All", "Can Be Delivered" };
        var index = Mathf.Clamp((int)currentFilter, 0, labels.Length - 1);
        var nextIndex = GUILayout.SelectionGrid(index, labels, 2);
        nextIndex = Mathf.Clamp(nextIndex, 0, labels.Length - 1);
        return (ContractDeliverabilityFilter)nextIndex;
    }

    private static ContractSpecificationFilter DrawContractSpecificationFilter(string label, ContractSpecificationFilter currentFilter)
    {
        GUILayout.Label(label);
        var labels = new[] { "All", "Have Matching Plane", "Don't Have Matching Plane" };
        var index = Mathf.Clamp((int)currentFilter, 0, labels.Length - 1);
        var nextIndex = GUILayout.SelectionGrid(index, labels, 1);
        nextIndex = Mathf.Clamp(nextIndex, 0, labels.Length - 1);
        return (ContractSpecificationFilter)nextIndex;
    }

    private static AircraftCategory DrawAircraftCategorySelector(string label, AircraftCategory currentCategory)
    {
        GUILayout.Label(label);
        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        var labels = profiles.Select(profile => profile.DisplayName).ToArray();
        var selectedIndex = 0;
        for (var index = 0; index < profiles.Count; index++)
        {
            if (profiles[index].Category == currentCategory)
            {
                selectedIndex = index;
                break;
            }
        }

        var nextIndex = GUILayout.SelectionGrid(selectedIndex, labels, 1);
        nextIndex = Mathf.Clamp(nextIndex, 0, profiles.Count - 1);
        return profiles[nextIndex].Category;
    }

    private static void DrawContractRequirements(ContractRequirements requirements)
    {
        if (requirements.RequiredCategory.HasValue)
        {
            var profile = AircraftCategoryCatalog.GetProfile(requirements.RequiredCategory.Value);
            GUILayout.Label($"Category: {profile.DisplayName}");
        }

        if (requirements.MinimumReliability.HasValue)
        {
            GUILayout.Label($"Reliability >= {FormatUiNumber(requirements.MinimumReliability.Value, "0.000")}");
        }

        if (requirements.MaximumTotalWeightKilograms.HasValue)
        {
            GUILayout.Label($"Weight <= {FormatUiNumber(requirements.MaximumTotalWeightKilograms.Value, "0.0")} kg");
        }

        if (requirements.MinimumRangeKilometers.HasValue)
        {
            GUILayout.Label($"Range >= {FormatUiNumber(requirements.MinimumRangeKilometers.Value, "0.0")} km");
        }

        if (requirements.MinimumSpeed.HasValue)
        {
            var speed = requirements.MinimumSpeed.Value;
            GUILayout.Label($"Speed @ {FormatUiNumber(speed.AltitudeMeters, "0")}m >= {FormatUiNumber(speed.MinimumSpeedMetersPerSecond * 3.6d, "0.0")} km/h");
        }

        if (requirements.MinimumPayloadKilograms.HasValue)
        {
            GUILayout.Label($"Payload >= {FormatUiNumber(requirements.MinimumPayloadKilograms.Value, "0.0")} kg");
        }

        if (requirements.MinimumCrew.HasValue)
        {
            GUILayout.Label($"Crew >= {FormatUiInteger(requirements.MinimumCrew.Value)}");
        }

        if (requirements.MaximumRequiredRunwayLengthMeters.HasValue)
        {
            GUILayout.Label($"Runway <= {FormatUiNumber(requirements.MaximumRequiredRunwayLengthMeters.Value, "0.0")} m");
        }
    }

    private static double CalculateFuelPerOutputMetric(double output, double fuelKilogramsPerHour, bool isJet)
    {
        var fuelDensityKilogramsPerLiter = isJet ? 0.8d : PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        var fuelLitersPerHour = fuelKilogramsPerHour / System.Math.Max(fuelDensityKilogramsPerLiter, 0.01d);
        return fuelLitersPerHour / System.Math.Max(output, 0.001d);
    }

    private static MaterialType DrawMaterialSelector(string label, MaterialType currentMaterial)
    {
        GUILayout.Label(label);
        var values = new[] { MaterialType.WoodFabric, MaterialType.Aluminum, MaterialType.Composite };
        var labels = new[] { "Wood", "Aluminum", "Composite" };
        var index = Array.IndexOf(values, currentMaterial);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, 3);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static WingPosition DrawWingPositionSelector(string label, WingPosition wingPosition)
    {
        GUILayout.Label(label);
        var values = new[] { WingPosition.Low, WingPosition.High, WingPosition.Biplane };
        var labels = new[] { "Low", "High", "Biplane" };
        var index = Array.IndexOf(values, wingPosition);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, 3);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static WingProfileType DrawWingProfileSelector(string label, WingProfileType wingProfile)
    {
        GUILayout.Label(label);
        var values = new[]
        {
            WingProfileType.Laminar6SeriesAerodynamic,
            WingProfileType.Naca4DigitLight,
            WingProfileType.SymmetricalAcrobatic,
        };
        var labels = new[]
        {
            "Laminar 6-series",
            "NACA 4-digit Light",
            "Symmetrical Acrobatic",
        };
        var index = Array.IndexOf(values, wingProfile);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, 2);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static TailType DrawTailSelector(string label, TailType tailType)
    {
        GUILayout.Label(label);
        var values = new[] { TailType.Classic, TailType.TTail, TailType.VTail, TailType.Twin };
        var labels = new[] { "Classic", "T tail", "V tail", "Twin" };
        var index = Array.IndexOf(values, tailType);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, 4);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static FlapType DrawTrailingFlapSelector(string label, FlapType flapType)
    {
        GUILayout.Label(label);
        var values = new[] { FlapType.None, FlapType.Plain, FlapType.Fowler };
        var labels = new[] { "None", "Plain", "Fowler" };
        var index = Array.IndexOf(values, flapType);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, 3);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static LandingGearType DrawLandingGearSelector(string label, LandingGearType gearType)
    {
        GUILayout.Label(label);
        var values = new[] { LandingGearType.Fixed, LandingGearType.SemiRetractable, LandingGearType.Retractable };
        var labels = new[] { "Fixed", "Semi Retractable", "Retractable" };
        var index = Array.IndexOf(values, gearType);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, 3);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static RunwayType DrawRunwayTypeSelector(string label, RunwayType runwayType)
    {
        GUILayout.Label(label);
        var values = new[] { RunwayType.Short, RunwayType.Medium, RunwayType.Long };
        var labels = new[] { "Short (500m)", "Medium (1000m)", "Long (2000m)" };
        var index = Array.IndexOf(values, runwayType);
        if (index < 0)
        {
            index = 1;
        }

        var next = GUILayout.SelectionGrid(index, labels, 3);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private static EngineDesignerFamily DrawEngineFamilySelector(string label, EngineDesignerFamily currentFamily)
    {
        GUILayout.Label(label);
        var labels = new[] { "Piston", "Jet" };
        var index = currentFamily == EngineDesignerFamily.Jet ? 1 : 0;
        var nextIndex = GUILayout.SelectionGrid(index, labels, 2);
        return nextIndex == 1 ? EngineDesignerFamily.Jet : EngineDesignerFamily.Piston;
    }

    private static AircraftAllowedEngineFamily DrawFamilyAllowedEngineSelector(string label, AircraftAllowedEngineFamily currentFamily)
    {
        GUILayout.Label(label);
        var labels = new[] { "Propeller", "Jet" };
        var index = currentFamily == AircraftAllowedEngineFamily.Jet ? 1 : 0;
        var nextIndex = GUILayout.SelectionGrid(index, labels, 2);
        return nextIndex == 1 ? AircraftAllowedEngineFamily.Jet : AircraftAllowedEngineFamily.Propeller;
    }

    private static EngineType DrawPistonArrangementSelector(string label, EngineType currentType)
    {
        GUILayout.Label(label);
        var values = new[] { EngineType.Inline, EngineType.V, EngineType.Radial };
        var labels = new[] { "Inline", "V", "Radial" };
        var index = Array.IndexOf(values, currentType);
        if (index < 0)
        {
            index = 2;
        }

        var nextIndex = GUILayout.SelectionGrid(index, labels, 3);
        nextIndex = Mathf.Clamp(nextIndex, 0, values.Length - 1);
        return values[nextIndex];
    }

    private static EngineType DrawJetTurbineTypeSelector(string label, EngineType currentType)
    {
        GUILayout.Label(label);
        var values = new[] { EngineType.Turbojet, EngineType.Turbofan };
        var labels = new[] { "Turbojet", "Turbofan" };
        var index = Array.IndexOf(values, currentType);
        if (index < 0)
        {
            index = 1;
        }

        var nextIndex = GUILayout.SelectionGrid(index, labels, 2);
        nextIndex = Mathf.Clamp(nextIndex, 0, values.Length - 1);
        return values[nextIndex];
    }

    private static BoostType DrawPistonBoostSelector(string label, BoostType currentBoost)
    {
        GUILayout.Label(label);
        var values = new[] { BoostType.None, BoostType.Supercharger, BoostType.Turbo };
        var labels = new[] { "Atmospheric", "Supercharger", "Turbocharger" };
        var index = Array.IndexOf(values, currentBoost);
        if (index < 0)
        {
            index = 0;
        }

        var nextIndex = GUILayout.SelectionGrid(index, labels, 3);
        nextIndex = Mathf.Clamp(nextIndex, 0, values.Length - 1);
        return values[nextIndex];
    }

    private static PistonInjectionType DrawPistonInjectionSelector(string label, PistonInjectionType currentInjectionType)
    {
        GUILayout.Label(label);
        var values = new[] { PistonInjectionType.Carburetor, PistonInjectionType.DirectInjection };
        var labels = new[] { "Carburetor", "Direct Injection" };
        var index = Array.IndexOf(values, currentInjectionType);
        if (index < 0)
        {
            index = 1;
        }

        var nextIndex = GUILayout.SelectionGrid(index, labels, 2);
        nextIndex = Mathf.Clamp(nextIndex, 0, values.Length - 1);
        return values[nextIndex];
    }

    private bool TryResolveEngineDefinitionForType(EngineType requestedType, out int selectedDefinitionIndex)
    {
        var candidates = new List<int>();
        for (var index = 0; index < _engineDefinitions.Count; index++)
        {
            if (_engineDefinitions[index].EngineType == requestedType)
            {
                candidates.Add(index);
            }
        }

        if (candidates.Count == 0)
        {
            for (var index = 0; index < _engineDefinitions.Count; index++)
            {
                var engineType = _engineDefinitions[index].EngineType;
                if (_engineDesignerFamily == EngineDesignerFamily.Piston && IsPistonFamily(engineType))
                {
                    candidates.Add(index);
                }
                else if (_engineDesignerFamily == EngineDesignerFamily.Jet && IsJetFamily(engineType))
                {
                    candidates.Add(index);
                }
            }
        }

        if (candidates.Count == 0)
        {
            selectedDefinitionIndex = 0;
            return false;
        }

        selectedDefinitionIndex = candidates.Contains(_selectedEngineDefinitionIndex)
            ? _selectedEngineDefinitionIndex
            : candidates[0];
        return true;
    }

    private void SyncEngineSelectorsFromDefinition(EngineType definitionType)
    {
        if (IsJetFamily(definitionType))
        {
            _engineDesignerFamily = EngineDesignerFamily.Jet;
            _selectedJetTurbineType = definitionType is EngineType.Turbojet or EngineType.Turbofan
                ? definitionType
                : EngineType.Turbofan;
            return;
        }

        _engineDesignerFamily = EngineDesignerFamily.Piston;
        _selectedPistonArrangement = definitionType switch
        {
            EngineType.Inline => EngineType.Inline,
            EngineType.V => EngineType.V,
            EngineType.Radial => EngineType.Radial,
            _ => EngineType.Radial,
        };
    }

    private static bool IsJetFamily(EngineType engineType) =>
        engineType is EngineType.Jet or EngineType.Turbojet or EngineType.Turbofan;

    private static bool IsPistonFamily(EngineType engineType) => !IsJetFamily(engineType);

    private static EnginePreviewKind GetEnginePreviewKind(EngineType engineType, double bypassRatio)
    {
        return engineType switch
        {
            EngineType.Inline => EnginePreviewKind.InlinePiston,
            EngineType.V => EnginePreviewKind.VPiston,
            EngineType.Radial => EnginePreviewKind.RadialPiston,
            EngineType.Piston => EnginePreviewKind.InlinePiston,
            EngineType.Turbofan => bypassRatio > 10d
                ? EnginePreviewKind.HighBypassTurbofan
                : EnginePreviewKind.LowBypassTurbofan,
            EngineType.Jet => EnginePreviewKind.Turbojet,
            EngineType.Turbojet => EnginePreviewKind.Turbojet,
            _ => EnginePreviewKind.InlinePiston,
        };
    }

    private void SyncTypeAirframeFromBase()
    {
        _typeCrewCount = _baseAirframeParameters.CrewCount;
        _typeTailType = _baseAirframeParameters.TailType;
        _typeWingShape = _baseAirframeParameters.WingShape;
        _typeWingProfile = _baseAirframeParameters.WingProfile == WingProfileType.Standard
            ? WingProfileType.Laminar6SeriesAerodynamic
            : _baseAirframeParameters.WingProfile;
        _typeWingAreaSquareMeters = _baseAirframeParameters.WingAreaSquareMeters;
        _typeFlapType = _baseAirframeParameters.FlapType == FlapType.LeadingEdge ? FlapType.Plain : _baseAirframeParameters.FlapType;
        _typeHasLeadingEdgeFlaps = _baseAirframeParameters.HasLeadingEdgeFlaps || _baseAirframeParameters.FlapType == FlapType.LeadingEdge;
        _typeHasBackupSystems = _baseAirframeParameters.HasBackupSystems;
        _typeLandingGearType = _baseAirframeParameters.LandingGearType;
        _typeRunwayType = _baseAirframeParameters.RunwayType;
        _typeCowling = _baseAirframeParameters.EngineCowling;
        _typeAerodynamicCleanlinessPercent = _baseAirframeParameters.AerodynamicCleanlinessPercent;
        _typeFuelLiters = _baseAirframeParameters.FuelAmountLiters;
    }

    private static EngineParameters CreateDefaultEngineParameters(EngineDefinition definition)
    {
        var isJet = definition.EngineType is EngineType.Jet or EngineType.Turbojet or EngineType.Turbofan;
        return new EngineParameters
        {
            DisplacementLiters = isJet ? 6d : 17d,
            CylinderCount = isJet ? 12 : 12,
            CompressionRatio = isJet ? 16d : 8.6d,
            BoostType = BoostType.None,
            PistonInjectionType = PistonInjectionType.DirectInjection,
            OptimizationAltitudeMeters = isJet ? 3600d : 1800d,
            TurbineDiameterMeters = isJet ? 1.35d : 1d,
            BypassRatio = definition.EngineType == EngineType.Turbofan ? 4.2d : 0d,
            MaximumSpeed = isJet ? 45000d : 2500d,
            CoolingEfficiency = 0.77d,
            EfficiencyOptimizationsPercent = EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent,
            HasAfterburner = false,
        };
    }

    private static AirframeParameters CreateDefaultAirframeParameters(int engineCount)
    {
        return new AirframeParameters
        {
            EngineCount = engineCount,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingProfile = WingProfileType.Standard,
            WingAreaSquareMeters = 36d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            HasLeadingEdgeFlaps = false,
            HasBackupSystems = false,
            LandingGearType = LandingGearType.SemiRetractable,
            RunwayType = RunwayType.Medium,
            EngineCowling = 0.55d,
            AerodynamicCleanlinessPercent = 0d,
            FuelAmountLiters = 1500d,
            CrewCount = 2,
        };
    }

    private static int FindIndexById(IReadOnlyList<string> ids, string preferredId)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], preferredId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static int NormalizeEngineCount(int value) => value >= 1 && value <= 6 ? value : 1;

    private string BuildFamilyId() => BuildNamedId("fam", _familyName, "player");

    private string BuildTypeId() => BuildNamedId("type", _selectedAircraftCategory.ToString(), "player");

    private static string BuildNamedId(string prefix, string name, string fallbackSuffix)
    {
        var sanitizedBody = SanitizeId(name, fallbackSuffix);
        if (sanitizedBody.StartsWith(prefix + ".", StringComparison.Ordinal))
        {
            return sanitizedBody;
        }

        return $"{prefix}.{sanitizedBody}";
    }

    private static string SanitizeId(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant().Replace(' ', '.');
    }

    private static string SanitizeName(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private string BuildTypeDisplayName() => AircraftCategoryCatalog.GetProfile(_selectedAircraftCategory).DisplayName;

    private static string GetAllowedEngineFamilyLabel(AircraftAllowedEngineFamily allowedEngineFamily)
    {
        return allowedEngineFamily switch
        {
            AircraftAllowedEngineFamily.Jet => "Jet",
            AircraftAllowedEngineFamily.Propeller => "Propeller",
            _ => "Mixed",
        };
    }

    private static string BuildFamilyDisplayName(
        string baseName,
        int engineCount,
        MaterialType materialType,
        StructureType structureType,
        AircraftAllowedEngineFamily allowedEngineFamily = AircraftAllowedEngineFamily.Any) =>
        $"{SanitizeName(baseName, "Player Family")} | {GetAllowedEngineFamilyLabel(allowedEngineFamily)} | {FormatUiInteger(engineCount)} engines | {GetMaterialDisplayName(materialType)} | {structureType}";

    private static string BuildEngineDisplayName(string baseName, EngineType engineType, EngineParameters parameters) =>
        $"{SanitizeName(baseName, "Prototype Engine")} | {BuildEngineDescriptor(engineType, parameters)}";

    private static string BuildEngineDescriptor(EngineType engineType, EngineParameters parameters)
    {
        if (IsJetFamily(engineType))
        {
            return $"{engineType} {FormatUiNumber(parameters.TurbineDiameterMeters * 100d, "0")} cm";
        }

        return $"{engineType} {FormatUiInteger(parameters.CylinderCount)}c";
    }

    private static string GetMaterialDisplayName(MaterialType materialType) =>
        materialType switch
        {
            MaterialType.WoodFabric => "Wood",
            MaterialType.Aluminum => "Aluminum",
            MaterialType.Composite => "Composite",
            _ => materialType.ToString(),
        };

    private static string FormatSimulationDate(SimulationDate date)
    {
        var monthName = date.Month switch
        {
            1 => "January",
            2 => "February",
            3 => "March",
            4 => "April",
            5 => "May",
            6 => "June",
            7 => "July",
            8 => "August",
            9 => "September",
            10 => "October",
            11 => "November",
            12 => "December",
            _ => date.Month.ToString(),
        };

        return $"{monthName} {date.Year}";
    }

    private static string FormatBuildManifestTimestamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        if (string.Equals(value, "runtime-generated", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime-generated";
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        return value;
    }

    private static double GetRunwayLengthMeters(RunwayType runwayType) =>
        runwayType switch
        {
            RunwayType.Short => PlaneDesignerBalance.Airframe.RunwayShortLengthMeters,
            RunwayType.Medium => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
            RunwayType.Long => PlaneDesignerBalance.Airframe.RunwayLongLengthMeters,
            _ => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
        };

    private static void DrawRect(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private enum EngineDesignerFamily
    {
        Piston = 0,
        Jet = 1,
    }

    private enum ContractDeliverabilityFilter
    {
        All = 0,
        CanBeDeliveredOnly = 1,
    }

    private enum ContractSpecificationFilter
    {
        All = 0,
        HaveMatchingPlane = 1,
        DoNotHaveMatchingPlane = 2,
    }

    private readonly struct ContractAvailabilitySnapshot
    {
        public ContractAvailabilitySnapshot(
            Contract contract,
            bool canBeDeliveredNow,
            bool hasMatchingPlaneBySpecification)
        {
            Contract = contract;
            CanBeDeliveredNow = canBeDeliveredNow;
            HasMatchingPlaneBySpecification = hasMatchingPlaneBySpecification;
        }

        public Contract Contract { get; }

        public bool CanBeDeliveredNow { get; }

        public bool HasMatchingPlaneBySpecification { get; }
    }

    private sealed class SavedEngineDesign
    {
        public SavedEngineDesign(string name, string researchId, Engine engine)
        {
            Name = name;
            ResearchId = researchId;
            Definition = engine.Definition;
            Parameters = engine.Parameters.Clone();
        }

        public string Name { get; }

        public string ResearchId { get; }

        public EngineDefinition Definition { get; }

        public EngineParameters Parameters { get; }
    }

    private sealed class SavedFamilyDesign
    {
        public SavedFamilyDesign(
            string name,
            string familyId,
            int lockedEngineCount,
            StructureType lockedStructureType,
            MaterialType lockedMaterialType,
            WingPosition wingPosition,
            AircraftAllowedEngineFamily allowedEngineFamily)
        {
            Name = name;
            FamilyId = familyId;
            LockedEngineCount = lockedEngineCount;
            LockedStructureType = lockedStructureType;
            LockedMaterialType = lockedMaterialType;
            WingPosition = wingPosition;
            AllowedEngineFamily = allowedEngineFamily;
        }

        public string Name { get; }

        public string FamilyId { get; }

        public int LockedEngineCount { get; }

        public StructureType LockedStructureType { get; }

        public MaterialType LockedMaterialType { get; }

        public WingPosition WingPosition { get; }

        public AircraftAllowedEngineFamily AllowedEngineFamily { get; }
    }

    private sealed class SavedAircraftDesign
    {
        public SavedAircraftDesign(
            string name,
            FactoryAircraftTypeCatalogItem catalogItem,
            string engineResearchId,
            string familyResearchId,
            string typeResearchId)
        {
            Name = name;
            CatalogItem = catalogItem;
            EngineResearchId = engineResearchId;
            FamilyResearchId = familyResearchId;
            TypeResearchId = typeResearchId;
        }

        public string Name { get; }

        public FactoryAircraftTypeCatalogItem CatalogItem { get; }

        public string EngineResearchId { get; }

        public string FamilyResearchId { get; }

        public string TypeResearchId { get; }
    }
    private enum RuntimeScreen
    {
        Intro = 0,
        LoadMenu = 1,
        OptionsMenu = 2,
        Gameplay = 3,
    }

    private readonly struct ResolutionOption
    {
        public ResolutionOption(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public string Label => $"{Width} x {Height}";
    }

    private sealed class RunnerGameSavePayload
    {
        public int Version { get; set; }

        public string PlayerCompanyName { get; set; } = string.Empty;

        public int ElapsedMonths { get; set; }

        public int CurrentYear { get; set; }

        public int CurrentMonth { get; set; }

        public int Tab { get; set; }

        public int FinanceSubTab { get; set; }

        public int ResearchSubTab { get; set; }

        public int SelectedEngineDefinitionIndex { get; set; }

        public int SelectedAirframeDefinitionIndex { get; set; }

        public int SelectedSavedEngineIndex { get; set; }

        public int SelectedSavedFamilyIndex { get; set; }

        public int SelectedSavedAircraftDesignIndex { get; set; }

        public int LockedEngineCount { get; set; }

        public EngineDesignerFamily EngineDesignerFamily { get; set; }

        public AircraftAllowedEngineFamily FamilyAllowedEngineFamily { get; set; }

        public EngineType SelectedPistonArrangement { get; set; }

        public EngineType SelectedJetTurbineType { get; set; }

        public StructureType LockedStructure { get; set; }

        public MaterialType LockedMaterial { get; set; }

        public AircraftCategory SelectedAircraftCategory { get; set; }

        public string EngineDesignName { get; set; } = string.Empty;

        public string AircraftDesignName { get; set; } = string.Empty;

        public string FamilyName { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public int RequiredLineToolLevel { get; set; }

        public int TypeCrewCount { get; set; }

        public TailType TypeTailType { get; set; }

        public WingShape TypeWingShape { get; set; }

        public WingProfileType TypeWingProfile { get; set; }

        public double TypeWingAreaSquareMeters { get; set; }

        public FlapType TypeFlapType { get; set; }

        public bool TypeHasLeadingEdgeFlaps { get; set; }

        public bool TypeHasBackupSystems { get; set; }


        public LandingGearType TypeLandingGearType { get; set; }

        public RunwayType TypeRunwayType { get; set; }

        public double TypeCowling { get; set; }

        public double TypeAerodynamicCleanlinessPercent { get; set; }

        public double TypeFuelLiters { get; set; }

        public ContractDeliverabilityFilter ContractDeliverabilityFilter { get; set; }

        public ContractSpecificationFilter ContractSpecificationFilter { get; set; }

        public int ContractQuantityFilterMin { get; set; }

        public int ContractQuantityFilterMax { get; set; }

        public NotebookWindowSaveState GovernmentContractNotebookState { get; set; } = new();

        public EngineParameters EngineParameters { get; set; } = new();

        public AirframeParameters BaseAirframeParameters { get; set; } = new();

        public List<SavedEngineDesignState> SavedEngines { get; set; } = new();

        public List<SavedFamilyDesignState> SavedFamilies { get; set; } = new();

        public List<SavedAircraftDesignState> SavedAircraftDesigns { get; set; } = new();

        public List<ResearchModifierStateEntry> ResearchModifiersByResearchId { get; set; } = new();

        public List<StringStringStateEntry> SelectedWarehouseDesignByContractId { get; set; } = new();

        public List<StringDoubleStateEntry> DeliveredContractProfitById { get; set; } = new();

        public ContractBoardSnapshot? ContractBoardSnapshot { get; set; }

        public ResearchBoardSnapshot? ResearchBoardSnapshot { get; set; }

        public FactoryStateSnapshot? FactoryStateSnapshot { get; set; }

        public AccountingSystemSnapshot? AccountingSnapshot { get; set; }

        public string? ContractActionMessage { get; set; }

        public string? FactoryActionMessage { get; set; }

        public string? ResearchActionMessage { get; set; }
    }

    private sealed class NotebookWindowSaveState
    {
        public bool IsOpen { get; set; }

        public float PositionX { get; set; }

        public float PositionY { get; set; }

        public float SizeX { get; set; }

        public float SizeY { get; set; }

        public LegacyVector2State? Position { get; set; }

        public LegacyVector2State? Size { get; set; }
    }

    private sealed class LegacyVector2State
    {
        public float X { get; set; }

        public float Y { get; set; }
    }

    private sealed class SavedEngineDesignState
    {
        public string Name { get; set; } = string.Empty;

        public string ResearchId { get; set; } = string.Empty;

        public string EngineDefinitionId { get; set; } = string.Empty;

        public EngineParameters Parameters { get; set; } = new();
    }

    private sealed class SavedFamilyDesignState
    {
        public string Name { get; set; } = string.Empty;

        public string FamilyId { get; set; } = string.Empty;

        public int LockedEngineCount { get; set; }

        public StructureType LockedStructureType { get; set; }

        public MaterialType LockedMaterialType { get; set; }

        public WingPosition WingPosition { get; set; }

        public AircraftAllowedEngineFamily AllowedEngineFamily { get; set; }
    }

    private sealed class SavedAircraftDesignState
    {
        public string Name { get; set; } = string.Empty;

        public string EngineResearchId { get; set; } = string.Empty;

        public string FamilyResearchId { get; set; } = string.Empty;

        public string TypeResearchId { get; set; } = string.Empty;

        public string DesignId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int RequiredFactoryToolLevel { get; set; }

        public int RequiredLineToolLevel { get; set; }

        public AircraftFamilyDefinition? FamilyDefinition { get; set; }

        public AircraftTypeDefinition? TypeDefinition { get; set; }
    }

    private sealed class ResearchModifierStateEntry
    {
        public string ResearchId { get; set; } = string.Empty;

        public int ReliabilityPoints { get; set; }

        public int ProductionOptimizationPoints { get; set; }

        public int FastDevelopmentPoints { get; set; }
    }

    private sealed class StringStringStateEntry
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }

    private sealed class StringDoubleStateEntry
    {
        public string Key { get; set; } = string.Empty;

        public double Value { get; set; }
    }
}












