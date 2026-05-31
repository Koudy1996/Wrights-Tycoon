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
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Research;
using WrightTycoon.Game.Campaign;
using WrightTycoon.Game.GovernmentContracts;
using WrightTycoon.Game.RuntimeData;
using WrightTycoon.Game.UI;
using WrightTycoon.Game.Save;

namespace WrightTycoon.Game.Launch;

public sealed class WrightTycoonLaunchRunner : MonoBehaviour
{
    private const int FixedFactoryToolRequirementLevel = 1;
    private const int ModernCampaignStartYear = 2020;
    private const int PastOrdersWindowId = 9401;
    private const string HistoricalCampaignBonusContractBatchEffectType = "bonus_contract_batch";
    private const double VersionDevelopmentResearchCostMultiplier = 0.1d;
    private const double VersionDevelopmentResearchTimeMultiplier = 0.1d;
    private const double VersionDevelopmentTechnologyProgressWeight = 0.5d;
    private const double ExponentialSliderPower = 2d;
    private const double TypeWingAreaMinSquareMeters = 1d;
    private const double TypeWingAreaMaxSquareMeters = 1000d;
    private const float ModifierTooltipHoverDelaySeconds = 0.5f;
    private const float ModifierTooltipHoverRadiusPixels = 26f;
    private const float ModifierTooltipIconSize = 18f;

    [Header("Default Definition IDs")]
    [SerializeField] private string _engineDefinitionId = "eng.piston.sport";
    [SerializeField] private string _airframeDefinitionId = "air.sport.light";

    [Header("Family Defaults")]
    [SerializeField] private int _defaultEngineCount = 1;
    [SerializeField] private double _baseDevelopmentCost = 92000d;
    [SerializeField] private double _baseDevelopmentManDays = 240d;

    private readonly int[] _engineCountOptions = { 1, 2, 3, 4, 5, 6 };
    private readonly string[] _tabs = { "Engine Designer", "Airframe + Type Designer", "My machines", "Engineering", "Demand", "Factory", "Contracts", "Government Contracts", "Finance" };
    private readonly string[] _financeSubTabs = { "Income / Expense", "Last Month Details", "Transactions" };
    private readonly string[] _researchSubTabs = { "Current Engineering", "For Approval" };
    private readonly string[] _myMachinesSubTabs = { "Engines", "Families + Types" };
    private readonly IsaAtmosphereModel _atmosphereModel = new();
    private readonly AerodynamicsCalculator _aerodynamicsCalculator = new();
    private readonly CategoryComplianceEvaluator _categoryComplianceEvaluator = new();
    private readonly List<SavedEngineDesign> _savedEngines = new();
    private readonly List<SavedFamilyDesign> _savedFamilies = new();
    private readonly List<SavedAircraftDesign> _savedAircraftDesigns = new();
    private readonly List<Contract> _lastGeneratedContracts = new();
    private readonly Dictionary<string, string> _selectedWarehouseDesignByContractId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _excludedWarehouseDesignByContractId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _deliveredContractProfitById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _governmentDeliveryQuantityInputById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _factoryScrapQuantityInputByDesignId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _factoryTotalTargetInputByLineId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeResearchModifierPoints> _researchModifiersByResearchId = new(StringComparer.Ordinal);
    private readonly HistoricalCampaignState _historicalCampaignState = new();
    private readonly List<HistoricalCampaignArchiveEntry> _historicalCampaignArchive = new();
    private readonly HistoricalCampaignTechnologyProgression _historicalCampaignTechnologyProgression = new();

    private readonly FactoryAccountingCalculator _factoryAccountingCalculator = new();
    private readonly ContractPricingCalculator _contractPricingCalculator = new();
    private readonly ResearchCalculator _researchCalculator = new();
    private readonly AircraftPreview3DRenderer _aircraftPreview3DRenderer = new();
    private readonly HistoricalCampaignImageLoader _historicalCampaignImageLoader = new();
    private readonly HistoricalCampaignPopupWindow _historicalCampaignPopupWindow = new();

    private EngineBuilder? _engineBuilder;
    private AirframeBuilder? _airframeBuilder;
    private AircraftFactory? _aircraftFactory;
    private HistoricalCampaignService? _historicalCampaign;
    private HistoricalCampaignRuntimeContractModifierSource? _historicalCampaignContractModifierSource;
    private HistoricalCampaignRuntimeTechnologyModifierSource? _historicalCampaignTechnologyModifierSource;
    private ContractBoard? _contractBoard;
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
    private string _campaignActionMessage = string.Empty;
    private GovernmentContractNotebookWindow? _governmentContractNotebookWindow;
    private ContractUIState _governmentContractNotebookState = CreateDefaultGovernmentContractNotebookState();
    private string _factoryActionMessage = string.Empty;
    private string _researchActionMessage = string.Empty;
    private string _initError = string.Empty;

    private int _tab;
    private int _financeSubTab;
    private int _researchSubTab;
    private int _myMachinesSubTab;
    private int _selectedEngineDefinitionIndex;
    private int _selectedAirframeDefinitionIndex;
    private int _selectedSavedEngineIndex = -1;
    private int _selectedSavedFamilyIndex = -1;
    private int _selectedSavedAircraftDesignIndex = -1;
    private int _engineCompareReferenceIndex = -1;
    private int _typeCompareReferenceIndex = -1;
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
    private int _lastLoggedTechnologyModifierYear = int.MinValue;
    private ContractDeliverabilityFilter _contractDeliverabilityFilter = ContractDeliverabilityFilter.All;
    private ContractSpecificationFilter _contractSpecificationFilter = ContractSpecificationFilter.All;
    private bool _contractRequestedCategoryFilterEnabled;
    private AircraftCategory _contractRequestedCategoryFilter = AircraftCategory.LightSportAndRecreational;
    private int _contractQuantityFilterMin = 5;
    private int _contractQuantityFilterMax = 500;
    private EngineVersionDevelopmentContext? _engineVersionDevelopment;
    private TypeVersionDevelopmentContext? _typeVersionDevelopment;

    private bool _initialized;
    private bool _showHistoricalArchive;
    private bool _showPastOrdersWindow;
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;
    private Vector2 _historicalCampaignArchiveScroll;
    private Vector2 _pastOrdersScroll;
    private Rect _pastOrdersWindowRect = new(120f, 100f, 980f, 620f);
    private bool _showHistoricalCampaignDebugPanel;
    private bool _showPendingProposalAdvanceDialog;
    private bool _disablePendingProposalAdvancePrompt;
    private string _historicalCampaignDebugYearInput = string.Empty;
    private string _historicalCampaignDebugMonthInput = string.Empty;
    private string _historicalCampaignDebugMessage = string.Empty;
    private readonly Dictionary<string, string> _doubleSliderInputBuffer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _intSliderInputBuffer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _doubleSliderHadFocus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _intSliderHadFocus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModifierTooltipFrameEntry> _modifierTooltipEntries = new(StringComparer.Ordinal);
    private bool _hasModifierTooltipHoverCandidate;
    private string _modifierTooltipHoverCandidateId = string.Empty;
    private float _modifierTooltipHoverCandidateDistance = float.MaxValue;
    private string _modifierTooltipTrackedId = string.Empty;
    private float _modifierTooltipTrackedSinceTime;
    private string _modifierTooltipVisibleId = string.Empty;
    private Texture2D? _modifierTooltipCircleTexture;
    private GUIStyle? _modifierTooltipQuestionStyle;
    private GUIStyle? _modifierTooltipTitleStyle;
    private GUIStyle? _modifierTooltipBodyStyle;

    private const string IntroBackgroundResourcePath = "WT04/Menu/main_menu_background";
    private const string IntroLogoResourcePath = "WT04/Menu/main_menu_logo";
    private const string SaveFileName = "saves.json";
    private const int AutosaveIntervalMonths = 6;
    private const int AutosaveRetentionCount = 4;
    private const int SavePayloadVersion = 2;

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
    private static readonly ModifierTooltipDefinition AllowedEngineFamilyPropellerTooltip = new(
        "modifier.allowed.engine.family",
        "Engine Family: Propeller",
        neutralLines: new[] { "No +/- notes are listed for engine family in the source sheet." });
    private static readonly ModifierTooltipDefinition AllowedEngineFamilyJetTooltip = new(
        "modifier.allowed.engine.family",
        "Engine Family: Jet",
        neutralLines: new[] { "No +/- notes are listed for engine family in the source sheet." });
    private static readonly ModifierTooltipDefinition BoostNoneTooltip = new(
        "modifier.engine.boost",
        "Boost: None",
        positiveLines: new[] { "Simple, reliable" },
        negativeLines: new[] { "Less power at altitude" });
    private static readonly ModifierTooltipDefinition BoostSuperchargerTooltip = new(
        "modifier.engine.boost",
        "Boost: Supercharger",
        positiveLines: new[] { "More power, good mid-altitude" },
        negativeLines: new[] { "Higher weight, reliability penalty" });
    private static readonly ModifierTooltipDefinition BoostTurboTooltip = new(
        "modifier.engine.boost",
        "Boost: Turbo",
        positiveLines: new[] { "Strong all-altitude power" },
        negativeLines: new[] { "Higher frontal area, weight, reliability penalty" });
    private static readonly ModifierTooltipDefinition InjectionCarburetorTooltip = new(
        "modifier.engine.injection",
        "Injection: Carburetor",
        positiveLines: new[] { "Cheap, short research" },
        negativeLines: new[] { "Lower power" });
    private static readonly ModifierTooltipDefinition InjectionDirectTooltip = new(
        "modifier.engine.injection",
        "Injection: Direct Injection",
        positiveLines: new[] { "Better power output" },
        negativeLines: new[] { "Higher cost and production time" });
    private static readonly ModifierTooltipDefinition PistonCompressionTooltip = new(
        "modifier.engine.compression.piston",
        "Compression (Piston Engine)",
        positiveLines: new[] { "Higher power, better fuel economy" },
        negativeLines: new[] { "Higher cost, higher weight, lower reliability" });
    private static readonly ModifierTooltipDefinition JetCompressionTooltip = new(
        "modifier.engine.compression.jet",
        "Compression (Jet Engine)",
        positiveLines: new[] { "Higher power, better fuel economy" },
        negativeLines: new[] { "Higher cost, higher weight, lower reliability" });
    private static readonly ModifierTooltipDefinition AfterburnerTooltip = new(
        "modifier.engine.afterburner",
        "Jet Afterburner",
        positiveLines: new[] { "Much higher thrust" },
        negativeLines: new[] { "More weight, cost, reliability penalty" });
    private static readonly ModifierTooltipDefinition StructureTrussTooltip = new(
        "modifier.family.structure",
        "Structure: Truss",
        positiveLines: new[] { "Cheap, early, simple, best for small planes" },
        negativeLines: new[] { "Heavier, less advanced" });
    private static readonly ModifierTooltipDefinition StructureMonocoqueTooltip = new(
        "modifier.family.structure",
        "Structure: Monocoque",
        positiveLines: new[] { "Cleaner, stronger design" },
        negativeLines: new[] { "Higher complexity" });
    private static readonly ModifierTooltipDefinition MaterialWoodFabricTooltip = new(
        "modifier.family.material",
        "Material: Wood/Fabric",
        positiveLines: new[] { "Cheap and light for early planes" },
        negativeLines: new[] { "Weak for advanced, heavy designs" });
    private static readonly ModifierTooltipDefinition MaterialAluminumTooltip = new(
        "modifier.family.material",
        "Material: Aluminum",
        positiveLines: new[] { "Good all-round structure" });
    private static readonly ModifierTooltipDefinition MaterialCompositeTooltip = new(
        "modifier.family.material",
        "Material: Composite",
        positiveLines: new[] { "Advanced, light, efficient" },
        negativeLines: new[] { "Late unlock, expensive, complex" });
    private static readonly ModifierTooltipDefinition WingPositionLowTooltip = new(
        "modifier.airframe.wing.position",
        "Wing Position: Low",
        positiveLines: new[] { "Lower drag, faster" },
        negativeLines: new[] { "Less lift, maneuverability" });
    private static readonly ModifierTooltipDefinition WingPositionHighTooltip = new(
        "modifier.airframe.wing.position",
        "Wing Position: High",
        positiveLines: new[] { "Better lift, lighter" },
        negativeLines: new[] { "More drag" });
    private static readonly ModifierTooltipDefinition WingPositionBiplaneTooltip = new(
        "modifier.airframe.wing.position",
        "Wing Position: Biplane",
        positiveLines: new[] { "Very high lift, light" },
        negativeLines: new[] { "Slow, high drag" });
    private static readonly ModifierTooltipDefinition WingShapeStraightTooltip = new(
        "modifier.airframe.wing.shape",
        "Wing Shape: Straight",
        positiveLines: new[] { "Stable, simple" },
        negativeLines: new[] { "Lower high-speed potential" });
    private static readonly ModifierTooltipDefinition WingShapeSweptTooltip = new(
        "modifier.airframe.wing.shape",
        "Wing Shape: Swept",
        positiveLines: new[] { "Better at higher speed, lower drag" },
        negativeLines: new[] { "More complex, less low-speed performance" });
    private static readonly ModifierTooltipDefinition WingShapeDeltaTooltip = new(
        "modifier.airframe.wing.shape",
        "Wing Shape: Delta",
        positiveLines: new[] { "Strong high-speed layout" },
        negativeLines: new[] { "Lower lift" });
    private static readonly ModifierTooltipDefinition WingProfileLaminarTooltip = new(
        "modifier.airframe.wing.profile",
        "Wing Profile: Laminar 6-series",
        positiveLines: new[] { "Good lift, low drag" },
        negativeLines: new[] { "Best on big wings" });
    private static readonly ModifierTooltipDefinition WingProfileNacaTooltip = new(
        "modifier.airframe.wing.profile",
        "Wing Profile: NACA 4-digit Light",
        positiveLines: new[] { "Lighter, cheaper" },
        negativeLines: new[] { "Drag rises at speed" });
    private static readonly ModifierTooltipDefinition WingProfileSymmetricalTooltip = new(
        "modifier.airframe.wing.profile",
        "Wing Profile: Symmetrical Acrobatic",
        positiveLines: new[] { "Better maneuverability" },
        negativeLines: new[] { "Less lift, higher cost" });
    private static readonly ModifierTooltipDefinition FlapNoneTooltip = new(
        "modifier.airframe.flaps",
        "Flaps: None",
        positiveLines: new[] { "Light, simple" },
        negativeLines: new[] { "Poor low-speed handling" });
    private static readonly ModifierTooltipDefinition FlapPlainTooltip = new(
        "modifier.airframe.flaps",
        "Flaps: Plain",
        positiveLines: new[] { "Better takeoff, landing" },
        negativeLines: new[] { "Adds drag, complexity" });
    private static readonly ModifierTooltipDefinition FlapFowlerTooltip = new(
        "modifier.airframe.flaps",
        "Flaps: Fowler",
        positiveLines: new[] { "Strong lift improvement" },
        negativeLines: new[] { "Heavier, more expensive" });
    private static readonly ModifierTooltipDefinition LeadingEdgeFlapsTooltip = new(
        "modifier.airframe.leading.edge.flaps",
        "Leading Edge Flaps",
        positiveLines: new[] { "Better lift and maneuverability" },
        negativeLines: new[] { "More cost, weight, reliability penalty" });
    private static readonly ModifierTooltipDefinition BackupSystemsTooltip = new(
        "modifier.airframe.backup.systems",
        "Backup Systems",
        positiveLines: new[] { "Much better reliability" },
        negativeLines: new[] { "More weight and drag" });
    private static readonly ModifierTooltipDefinition GearFixedTooltip = new(
        "modifier.airframe.gear",
        "Gear: Fixed",
        positiveLines: new[] { "Cheap, reliable" },
        negativeLines: new[] { "High drag" });
    private static readonly ModifierTooltipDefinition GearSemiRetractableTooltip = new(
        "modifier.airframe.gear",
        "Gear: Semi Retractable",
        positiveLines: new[] { "Balanced drag, reliability" },
        negativeLines: new[] { "More cost and complexity" });
    private static readonly ModifierTooltipDefinition GearRetractableTooltip = new(
        "modifier.airframe.gear",
        "Gear: Retractable",
        positiveLines: new[] { "Lowest drag, faster" },
        negativeLines: new[] { "Costly, less reliable" });
    private static readonly ModifierTooltipDefinition RunwayShortTooltip = new(
        "modifier.airframe.runway",
        "Runway: Short",
        positiveLines: new[] { "More contract flexibility" },
        negativeLines: new[] { "Lower performance" });
    private static readonly ModifierTooltipDefinition RunwayMediumTooltip = new(
        "modifier.airframe.runway",
        "Runway: Medium",
        positiveLines: new[] { "Balanced operations" },
        negativeLines: new[] { "Less flexible than short" });
    private static readonly ModifierTooltipDefinition RunwayLongTooltip = new(
        "modifier.airframe.runway",
        "Runway: Long",
        positiveLines: new[] { "Good for heavy aircraft" },
        negativeLines: new[] { "Needs longer runways" });

    private GameSavePersistenceService? _savePersistence;
    private WrightTycoonBuildManifest _buildManifest = new();
    private Texture2D? _introBackgroundTexture;
    private Texture2D? _introLogoTexture;
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
        _historicalCampaignImageLoader.Dispose();
        if (_modifierTooltipCircleTexture is not null)
        {
            Destroy(_modifierTooltipCircleTexture);
            _modifierTooltipCircleTexture = null;
        }
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
            ClearModifierTooltipInteraction();
            DrawIntroScreen(virtualScreenSize);
            return;
        }

        if (_runtimeScreen == RuntimeScreen.LoadMenu)
        {
            ClearModifierTooltipInteraction();
            DrawLoadMenuScreen(virtualScreenSize);
            return;
        }

        if (_runtimeScreen == RuntimeScreen.OptionsMenu)
        {
            ClearModifierTooltipInteraction();
            DrawOptionsScreen(virtualScreenSize);
            return;
        }

        BeginModifierTooltipFrame();

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

        var previousGuiEnabled = GUI.enabled;
        if (_showPendingProposalAdvanceDialog)
        {
            GUI.enabled = false;
        }

        var selectedTab = GUILayout.Toolbar(_tab, _tabs, GUILayout.Height(34f));
        if (selectedTab != _tab)
        {
            _showHistoricalArchive = false;
        }

        _tab = selectedTab;
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label($"Date: {FormatSimulationDate(_currentDate)}", GUILayout.Width(220f));
        GUILayout.Label($"Company: {_playerCompanyName}", GUILayout.Width(220f));
        if (GUILayout.Button("Next Month", GUILayout.Width(150f), GUILayout.Height(26f)))
        {
            HandleNextMonthButtonPressed();
        }
        if (GUILayout.Button("Historical Archive", GUILayout.Width(170f), GUILayout.Height(26f)))
        {
            _showHistoricalArchive = true;
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save and Exit", GUILayout.Width(160f), GUILayout.Height(26f)))
        {
            SaveAndExitToMainMenu();
        }

        var cash = _accountingSystem?.Cash ?? 0d;
        DrawSignedCurrencyInline($"Cash: {FormatCurrency(cash)}", cash, 220f);
        GUILayout.EndHorizontal();

        var historicalCampaignStatus = BuildHistoricalCampaignStatusLabel();
        if (!string.IsNullOrWhiteSpace(historicalCampaignStatus))
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(historicalCampaignStatus);
            GUILayout.EndHorizontal();
        }

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
        if (_showHistoricalArchive)
        {
            DrawHistoricalArchiveTab();
        }
        else if (_tab == 0)
        {
            DrawEngineTab();
        }
        else if (_tab == 1)
        {
            DrawAirframeTab();
        }
        else if (_tab == 2)
        {
            DrawMyMachinesTab();
        }
        else if (_tab == 3)
        {
            DrawResearchTab();
        }
        else if (_tab == 4)
        {
            DrawDemandTab();
        }
        else if (_tab == 5)
        {
            DrawFactoryTab();
        }
        else if (_tab == 6)
        {
            DrawContractsTab();
        }
        else if (_tab == 7)
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
        if (_showHistoricalArchive)
        {
            DrawHistoricalArchiveReport();
        }
        else if (_tab == 0)
        {
            DrawEngineReport();
        }
        else if (_tab == 1)
        {
            DrawAirframeReport();
        }
        else if (_tab == 2)
        {
            DrawMyMachinesReport();
        }
        else if (_tab == 3)
        {
            DrawResearchReport();
        }
        else if (_tab == 4)
        {
            DrawDemandReport();
        }
        else if (_tab == 5)
        {
            DrawFactoryReport();
        }
        else if (_tab == 6)
        {
            DrawContractsReport();
        }
        else if (_tab == 7)
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
        GUI.enabled = previousGuiEnabled;

        DrawModifierTooltipOverlay(virtualScreenSize);
        DrawPastOrdersWindow(virtualScreenSize);
        _historicalCampaignPopupWindow.Draw(virtualScreenSize);
        DrawPendingProposalAdvanceDialog(virtualScreenSize);
    }

    private void HandleNextMonthButtonPressed()
    {
        if (ShouldShowPendingProposalAdvanceDialog())
        {
            _showPendingProposalAdvanceDialog = true;
            return;
        }

        AdvanceToNextMonth();
    }

    private bool ShouldShowPendingProposalAdvanceDialog()
    {
        if (_disablePendingProposalAdvancePrompt || _researchBoard is null)
        {
            return false;
        }

        return CountVisiblePendingResearchProposals() > 0;
    }

    private int CountVisiblePendingResearchProposals() =>
        GetVisibleResearchProjectsByStatus(ResearchStatus.Proposed).Count;

    private void DrawPendingProposalAdvanceDialog(Vector2 virtualScreenSize)
    {
        if (!_showPendingProposalAdvanceDialog)
        {
            return;
        }

        var pendingProposalCount = CountVisiblePendingResearchProposals();
        if (pendingProposalCount <= 0)
        {
            _showPendingProposalAdvanceDialog = false;
            return;
        }

        var overlayRect = new Rect(0f, 0f, virtualScreenSize.x, virtualScreenSize.y);
        DrawRect(overlayRect, new Color(0f, 0f, 0f, 0.62f));

        var dialogWidth = Mathf.Min(620f, virtualScreenSize.x - 40f);
        var dialogHeight = 190f;
        var dialogRect = new Rect(
            (virtualScreenSize.x - dialogWidth) * 0.5f,
            (virtualScreenSize.y - dialogHeight) * 0.5f,
            dialogWidth,
            dialogHeight);

        DrawRect(dialogRect, new Color(0.06f, 0.09f, 0.13f, 0.98f));
        GUILayout.BeginArea(dialogRect, GUI.skin.box);
        GUILayout.Label("Do you have proposals ready and not approved, do you want to continue");
        GUILayout.Label($"Pending proposals: {FormatUiInteger(pendingProposalCount)}");
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("NO", GUILayout.Height(30f)))
        {
            _showPendingProposalAdvanceDialog = false;
        }

        if (GUILayout.Button("YES", GUILayout.Height(30f)))
        {
            _showPendingProposalAdvanceDialog = false;
            AdvanceToNextMonth();
        }

        if (GUILayout.Button("YES, don't ask me again", GUILayout.Height(30f)))
        {
            _disablePendingProposalAdvancePrompt = true;
            _showPendingProposalAdvanceDialog = false;
            AdvanceToNextMonth();
        }

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
            && _historicalCampaign is not null
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

        var historicalCampaignPath = ResolveHistoricalCampaignPath();
        var historicalCampaignRepository = new HistoricalCampaignJsonRepository();
        if (historicalCampaignRepository.TryLoadFromFile(
                historicalCampaignPath,
                out var loadedCampaign,
                out var campaignLoadError))
        {
            _historicalCampaign = loadedCampaign;
            Debug.Log(
                $"[Campaign] Loaded {_historicalCampaign.ScheduledEvents.Count} scheduled events " +
                $"({_historicalCampaign.AllEvents.Count} definitions) from {historicalCampaignPath}.");
            for (var index = 0; index < _historicalCampaign.LoadWarnings.Count; index++)
            {
                Debug.LogWarning($"[Campaign] {_historicalCampaign.LoadWarnings[index]}");
            }
        }
        else
        {
            _historicalCampaign = null;
            _campaignActionMessage = $"Historical campaign disabled: {campaignLoadError}";
            Debug.LogError($"[Campaign] {_campaignActionMessage}");
        }

        _historicalCampaignTechnologyModifierSource = new HistoricalCampaignRuntimeTechnologyModifierSource(
            _historicalCampaignTechnologyProgression,
            () => _currentDate);
        _engineBuilder = new EngineBuilder(
            new EngineCalculator(),
            _atmosphereModel,
            _historicalCampaignTechnologyModifierSource);
        _airframeBuilder = new AirframeBuilder(
            new AirframeCalculator(_aerodynamicsCalculator, _atmosphereModel),
            _historicalCampaignTechnologyModifierSource);
        _aircraftFactory = new AircraftFactory(
            _engineBuilder,
            _airframeBuilder,
            new AircraftPerformanceCalculator(_atmosphereModel, _aerodynamicsCalculator),
            new DevelopmentCostCalculator(),
            _engineRepo,
            _airframeRepo);
    }

    private void ResetRuntimeStateForNewGame(SimulationDate? startDate = null)
    {
        _selectedAircraftCategory = AircraftCategory.LightSportAndRecreational;
        _selectedEngineDefinitionIndex = FindIndexById(_engineDefinitions.Select(d => d.Id).ToArray(), _engineDefinitionId);
        _selectedAirframeDefinitionIndex = FindIndexById(_airframeDefinitions.Select(d => d.Id).ToArray(), _airframeDefinitionId);
        _lockedEngineCount = NormalizeEngineCount(_defaultEngineCount);
        _lockedStructure = StructureType.Truss;
        _lockedMaterial = MaterialType.WoodFabric;
        _familyAllowedEngineFamily = AircraftAllowedEngineFamily.Propeller;
        SyncEngineSelectorsFromDefinition(_engineDefinitions[_selectedEngineDefinitionIndex].EngineType);

        _engineParameters = CreateDefaultEngineParameters(_engineDefinitions[_selectedEngineDefinitionIndex]);
        _baseAirframeParameters = CreateDefaultAirframeParameters(_lockedEngineCount);
        SyncTypeAirframeFromBase();
        RebuildEnginePreview();

        _historicalCampaignContractModifierSource = _historicalCampaign is null
            ? null
            : new HistoricalCampaignRuntimeContractModifierSource(_historicalCampaign, _historicalCampaignState, () => _currentDate);
        _contractBoard = new ContractBoard(
            new ContractGenerator(
                campaignModifierSource: _historicalCampaignContractModifierSource,
                currentDateProvider: () => _currentDate),
            new ContractEvaluator());
        _productionFactory = new AircraftProductionFactory();
        var governmentRandom = new SystemGovernmentContractRandomProvider();
        var governmentGenerator = new GovernmentContractGenerator(
            governmentRandom,
            campaignModifierSource: _historicalCampaignContractModifierSource);
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
        _excludedWarehouseDesignByContractId.Clear();
        _deliveredContractProfitById.Clear();
        _governmentDeliveryQuantityInputById.Clear();
        _factoryScrapQuantityInputByDesignId.Clear();
        _selectedSavedEngineIndex = -1;
        _selectedSavedFamilyIndex = -1;
        _selectedSavedAircraftDesignIndex = -1;
        _lastFactoryReport = null;
        _currentDate = startDate ?? SimulationDate.WrightTycoonStart;
        _elapsedMonths = 0;
        _lastLoggedTechnologyModifierYear = int.MinValue;
        _tab = 0;
        _showHistoricalArchive = false;
        _financeSubTab = 0;
        _researchSubTab = 0;
        _myMachinesSubTab = 0;
        _contractActionMessage = string.Empty;
        _governmentContractActionMessage = string.Empty;
        _governmentDeliveryActionMessage = string.Empty;
        if (_historicalCampaign is not null)
        {
            _campaignActionMessage = string.Empty;
        }
        _factoryActionMessage = string.Empty;
        _researchActionMessage = string.Empty;
        _saveActionMessage = string.Empty;
        _showPastOrdersWindow = false;
        _pastOrdersScroll = Vector2.zero;
        _showPendingProposalAdvanceDialog = false;
        _disablePendingProposalAdvancePrompt = false;
        _showHistoricalCampaignDebugPanel = false;
        _historicalCampaignDebugMessage = string.Empty;
        _contractDeliverabilityFilter = ContractDeliverabilityFilter.All;
        _contractSpecificationFilter = ContractSpecificationFilter.All;
        _contractRequestedCategoryFilterEnabled = false;
        _contractRequestedCategoryFilter = AircraftCategory.LightSportAndRecreational;
        _contractQuantityFilterMin = 5;
        _contractQuantityFilterMax = 500;
        _engineVersionDevelopment = null;
        _typeVersionDevelopment = null;
        _historicalCampaignState.ReplaceFiredEventIds(null);
        ApplyHistoricalCampaignEventsUpToCurrentDate();
        NormalizeSelectedAircraftCategoryForHistoricalCampaign();
        _historicalCampaignArchive.Clear();
        _historicalCampaignArchiveScroll = Vector2.zero;
        _historicalCampaignPopupWindow.Close();
        SyncHistoricalCampaignDebugInputsToCurrentDate();
        LogHistoricalCampaignTechnologyModifiersIfYearChanged();

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

    private void ApplyHistoricalCampaignEventsUpToCurrentDate()
    {
        if (_historicalCampaign is null)
        {
            return;
        }

        var currentMonthKey = HistoricalCampaignValidator.CreateMonthKey(_currentDate.Year, _currentDate.Month);
        var scheduledEvents = _historicalCampaign.ScheduledEvents;
        for (var index = 0; index < scheduledEvents.Count; index++)
        {
            var definition = scheduledEvents[index];
            var eventMonthKey = HistoricalCampaignValidator.CreateMonthKey(definition.Year, definition.Month);
            if (eventMonthKey > currentMonthKey)
            {
                continue;
            }

            _historicalCampaignState.TryMarkFired(definition.Id);
        }
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

        _introLogoTexture = Resources.Load<Texture2D>(IntroLogoResourcePath);
        if (_introLogoTexture is null)
        {
            Debug.LogWarning($"[Menu] Intro logo not found in Resources at '{IntroLogoResourcePath}'.");
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
        var panelHeight = Mathf.Min(548f, virtualScreenSize.y - 40f);
        var panelY = Mathf.Clamp(
            (virtualScreenSize.y - panelHeight) * 0.5f + 42f,
            20f,
            virtualScreenSize.y - panelHeight - 20f);
        var panelRect = new Rect(
            (virtualScreenSize.x - panelWidth) * 0.5f,
            panelY,
            panelWidth,
            panelHeight);

        var hasContinueSave = _savePersistence is not null && _savePersistence.TryGetContinueEntry(out _);

        DrawIntroLogo(virtualScreenSize, panelRect);
        DrawRect(panelRect, new Color(0.04f, 0.07f, 0.11f, 0.96f));
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

        if (GUILayout.Button("light campain", GUILayout.Height(44f)))
        {
            StartNewGameFromMenu();
        }

        if (GUILayout.Button("2020 Start", GUILayout.Height(44f)))
        {
            StartNewGameFromMenu(new SimulationDate(ModernCampaignStartYear, 1), "New 2020 campaign started.");
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

    private void DrawIntroLogo(Vector2 virtualScreenSize, Rect panelRect)
    {
        if (_introLogoTexture is null)
        {
            return;
        }

        var textureWidth = System.Math.Max(_introLogoTexture.width, 1);
        var textureHeight = System.Math.Max(_introLogoTexture.height, 1);
        var aspect = textureWidth / (float)textureHeight;

        var logoWidth = Mathf.Min(720f, virtualScreenSize.x - 80f);
        var logoHeight = logoWidth / aspect;

        var availableHeightAbovePanel = Mathf.Max(panelRect.y - 24f, 0f);
        if (logoHeight > availableHeightAbovePanel)
        {
            logoHeight = availableHeightAbovePanel;
            logoWidth = logoHeight * aspect;
        }

        if (logoWidth < 40f || logoHeight < 40f)
        {
            return;
        }

        var logoRect = new Rect(
            (virtualScreenSize.x - logoWidth) * 0.5f,
            panelRect.y - logoHeight - 12f,
            logoWidth,
            logoHeight);
        GUI.DrawTexture(logoRect, _introLogoTexture, ScaleMode.ScaleToFit, true);
    }

    private void DrawLoadMenuScreen(Vector2 virtualScreenSize)
    {
        DrawIntroBackground(virtualScreenSize);

        var root = new Rect(20f, 20f, virtualScreenSize.x - 40f, virtualScreenSize.y - 40f);
        DrawRect(root, new Color(0.05f, 0.08f, 0.12f, 0.96f));

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

        DrawRect(panelRect, new Color(0.04f, 0.07f, 0.11f, 0.96f));
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

        DrawRect(full, new Color(0f, 0f, 0f, 0.44f));
    }

    private void StartNewGameFromMenu()
    {
        StartNewGameFromMenu(SimulationDate.WrightTycoonStart, "light campain started.");
    }

    private void StartNewGameFromMenu(SimulationDate startDate, string message)
    {
        TryInitialize();
        if (!_initialized)
        {
            return;
        }

        ResetRuntimeStateForNewGame(startDate);
        _runtimeScreen = RuntimeScreen.Gameplay;
        if (_governmentContractBoard?.ActiveContract is not null)
        {
            OpenGovernmentContractNotebookWindow();
        }

        _saveActionMessage = message;
        Debug.Log($"[Save] {message}");
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

    private void SaveAndExitToMainMenu()
    {
        var previousMessage = _saveActionMessage;
        CreateManualSave();

        if (_saveActionMessage.StartsWith("Save failed:", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(_saveActionMessage, previousMessage, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(previousMessage))
        {
            return;
        }

        _runtimeScreen = RuntimeScreen.Intro;
        CloseGovernmentContractNotebookWindow();
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
            MyMachinesSubTab = _myMachinesSubTab,
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
            RequiredLineToolLevel = 1,
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
            ContractRequestedCategoryFilterEnabled = _contractRequestedCategoryFilterEnabled,
            ContractRequestedCategoryFilter = _contractRequestedCategoryFilter,
            ContractQuantityFilterMin = _contractQuantityFilterMin,
            ContractQuantityFilterMax = _contractQuantityFilterMax,
            DisablePendingProposalAdvancePrompt = _disablePendingProposalAdvancePrompt,
            GovernmentContractNotebookState = CreateNotebookWindowSaveState(_governmentContractNotebookState),
            FiredHistoricalCampaignEventIds = _historicalCampaignState.CreateSnapshot().ToList(),
            HistoricalCampaignArchiveEntries = _historicalCampaignArchive
                .Select(entry => new HistoricalCampaignArchiveEntry
                {
                    Title = entry.Title,
                    Year = entry.Year,
                    Month = entry.Month,
                })
                .ToList(),
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
                DevelopmentStartedYear = saved.DevelopmentStartedYear,
                VariantDevelopmentStartedYear = saved.VariantDevelopmentStartedYear,
                ApprovedYear = saved.ApprovedYear,
                FrozenYear = saved.FrozenYear,
                FrozenMonth = saved.FrozenMonth,
                VersionDevelopmentBaselineYear = saved.IsVersionDevelopment
                    ? saved.DevelopmentStartedYear
                    : null,
                SourceResearchId = saved.SourceResearchId,
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
                RequiredLineToolLevel = 1,
                FamilyDefinition = saved.CatalogItem.Aircraft.FamilyDefinition,
                TypeDefinition = saved.CatalogItem.Aircraft.TypeDefinition,
                DevelopmentStartedYear = saved.DevelopmentStartedYear,
                VariantDevelopmentStartedYear = saved.VariantDevelopmentStartedYear,
                ApprovedYear = saved.ApprovedYear,
                FrozenYear = saved.FrozenYear,
                FrozenMonth = saved.FrozenMonth,
                TypeVersionDevelopmentBaselineYear = saved.IsTypeVersionDevelopment
                    ? saved.DevelopmentStartedYear
                    : null,
                SourceTypeResearchId = saved.SourceTypeResearchId,
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

        foreach (var entry in _excludedWarehouseDesignByContractId.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value.Count == 0)
            {
                continue;
            }

            payload.ExcludedWarehouseDesignByContractId.Add(new ContractExcludedWarehouseDesignStateEntry
            {
                ContractId = entry.Key,
                DesignIds = entry.Value
                    .Where(designId => !string.IsNullOrWhiteSpace(designId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(designId => designId, StringComparer.Ordinal)
                    .ToList(),
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

        if (_governmentContractBoard is not null)
        {
            payload.GovernmentContractBoardSnapshot = _governmentContractBoard.CreateSnapshot();
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
        _showHistoricalArchive = false;
        _showPastOrdersWindow = false;
        _pastOrdersScroll = Vector2.zero;
        _financeSubTab = Mathf.Clamp(data.FinanceSubTab, 0, _financeSubTabs.Length - 1);
        _researchSubTab = Mathf.Clamp(data.ResearchSubTab, 0, _researchSubTabs.Length - 1);
        _myMachinesSubTab = Mathf.Clamp(data.MyMachinesSubTab, 0, _myMachinesSubTabs.Length - 1);

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
        _contractRequestedCategoryFilterEnabled = data.ContractRequestedCategoryFilterEnabled;
        _contractRequestedCategoryFilter = Enum.IsDefined(typeof(AircraftCategory), data.ContractRequestedCategoryFilter)
            ? data.ContractRequestedCategoryFilter
            : AircraftCategory.LightSportAndRecreational;
        _contractQuantityFilterMin = Mathf.Clamp(data.ContractQuantityFilterMin, 5, 500);
        _contractQuantityFilterMax = Mathf.Clamp(data.ContractQuantityFilterMax, 5, 500);
        _disablePendingProposalAdvancePrompt = data.DisablePendingProposalAdvancePrompt;
        _showPendingProposalAdvanceDialog = false;
        _governmentContractNotebookState = ReadNotebookWindowSaveState(data.GovernmentContractNotebookState);
        _historicalCampaignState.ReplaceFiredEventIds(data.FiredHistoricalCampaignEventIds);
        NormalizeSelectedAircraftCategoryForHistoricalCampaign();
        RestoreHistoricalCampaignArchive(data.HistoricalCampaignArchiveEntries);
        if (_historicalCampaign is not null)
        {
            _campaignActionMessage = string.Empty;
        }
        _historicalCampaignDebugMessage = string.Empty;
        _historicalCampaignPopupWindow.Close();
        SyncHistoricalCampaignDebugInputsToCurrentDate();
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
        if (data.SavedEngines is not null)
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
                    var legacyBaselineYear = NormalizeOptionalBaselineYear(state.VersionDevelopmentBaselineYear);
                    var developmentStartedYear = NormalizeRequiredYear(
                        state.DevelopmentStartedYear,
                        legacyBaselineYear ?? _currentDate.Year);
                    var variantDevelopmentStartedYear = NormalizeRequiredYear(
                        state.VariantDevelopmentStartedYear,
                        developmentStartedYear);
                    _savedEngines.Add(new SavedEngineDesign(
                        string.IsNullOrWhiteSpace(state.Name) ? "Loaded Engine" : state.Name,
                        string.IsNullOrWhiteSpace(state.ResearchId) ? CreateUniqueEngineResearchId(state.Name) : state.ResearchId,
                        definition,
                        parameters,
                        developmentStartedYear,
                        variantDevelopmentStartedYear,
                        legacyBaselineYear,
                        state.SourceResearchId ?? string.Empty,
                        state.ApprovedYear,
                        state.FrozenYear,
                        state.FrozenMonth));
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
                    var hasFrozenDate = TryCreateSimulationDate(state.FrozenYear, state.FrozenMonth, out var frozenDate);
                    var aircraft = hasFrozenDate
                        ? BuildAircraftForDate(state.FamilyDefinition, state.TypeDefinition, frozenDate)
                        : _aircraftFactory.CreateAircraft(state.FamilyDefinition, state.TypeDefinition);
                    var designId = string.IsNullOrWhiteSpace(state.DesignId) ? state.TypeDefinition.Id : state.DesignId;
                    var displayName = string.IsNullOrWhiteSpace(state.DisplayName) ? state.Name : state.DisplayName;
                    var catalogItem = new FactoryAircraftTypeCatalogItem(
                        designId,
                        displayName,
                        aircraft,
                        System.Math.Max(state.RequiredFactoryToolLevel, 1),
                        1);
                    var legacyBaselineYear = NormalizeOptionalBaselineYear(state.TypeVersionDevelopmentBaselineYear);
                    var developmentStartedYear = NormalizeRequiredYear(
                        state.DevelopmentStartedYear,
                        legacyBaselineYear ?? _currentDate.Year);
                    var variantDevelopmentStartedYear = NormalizeRequiredYear(
                        state.VariantDevelopmentStartedYear,
                        developmentStartedYear);

                    _savedAircraftDesigns.Add(new SavedAircraftDesign(
                        string.IsNullOrWhiteSpace(state.Name) ? displayName : state.Name,
                        catalogItem,
                        string.IsNullOrWhiteSpace(state.EngineResearchId) ? "engine:unknown" : state.EngineResearchId,
                        string.IsNullOrWhiteSpace(state.FamilyResearchId) ? "family:unknown" : state.FamilyResearchId,
                        string.IsNullOrWhiteSpace(state.TypeResearchId) ? "type:unknown" : state.TypeResearchId,
                        developmentStartedYear,
                        variantDevelopmentStartedYear,
                        legacyBaselineYear,
                        state.SourceTypeResearchId ?? string.Empty,
                        state.ApprovedYear,
                        state.FrozenYear,
                        state.FrozenMonth));
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

        _excludedWarehouseDesignByContractId.Clear();
        if (data.ExcludedWarehouseDesignByContractId is not null)
        {
            for (var index = 0; index < data.ExcludedWarehouseDesignByContractId.Count; index++)
            {
                var entry = data.ExcludedWarehouseDesignByContractId[index];
                if (entry is null || string.IsNullOrWhiteSpace(entry.ContractId) || entry.DesignIds is null || entry.DesignIds.Count == 0)
                {
                    continue;
                }

                var excludedSet = new HashSet<string>(StringComparer.Ordinal);
                for (var designIndex = 0; designIndex < entry.DesignIds.Count; designIndex++)
                {
                    var designId = entry.DesignIds[designIndex];
                    if (string.IsNullOrWhiteSpace(designId))
                    {
                        continue;
                    }

                    excludedSet.Add(designId);
                }

                if (excludedSet.Count > 0)
                {
                    _excludedWarehouseDesignByContractId[entry.ContractId] = excludedSet;
                }
            }
        }

        _deliveredContractProfitById.Clear();
        _governmentDeliveryQuantityInputById.Clear();
        _factoryScrapQuantityInputByDesignId.Clear();
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

        if (data.GovernmentContractBoardSnapshot is not null && _governmentContractBoard is not null)
        {
            _governmentContractBoard.RestoreSnapshot(data.GovernmentContractBoardSnapshot);
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
        BackfillApprovedYearsFromResearchBoard();

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
            PruneContractDeliverySelections(_contractBoard.OpenContracts, BuildProductionCatalog(includeOnlyResearched: false));
        }

        if (_governmentContractBoard is not null)
        {
            PruneGovernmentDeliveryQuantityInputs(_governmentContractBoard.ActiveDeliveryContracts);
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

        var activeEngineVersionDevelopment = _engineVersionDevelopment;
        var isVersionDevelopment = activeEngineVersionDevelopment is not null;
        if (isVersionDevelopment)
        {
            DrawSection("Developing New Engine Version");
            GUILayout.Label(
                $"Base engine: {activeEngineVersionDevelopment!.SourceDisplayName} | " +
                $"development start: {FormatUiInteger(activeEngineVersionDevelopment.SourceDevelopmentStartedYear)} | " +
                $"variant start: {FormatUiInteger(activeEngineVersionDevelopment.VariantDevelopmentStartedYear)}");
            GUILayout.Label("Fixed parameters: engine family/type and cylinder count.");

            if (GUILayout.Button("Cancel Version Development", GUILayout.Height(26f)))
            {
                _engineVersionDevelopment = null;
            }
        }

        _engineDesignName = DrawText("Design Name", _engineDesignName);
        EngineType requestedType;
        if (isVersionDevelopment)
        {
            requestedType = activeEngineVersionDevelopment!.LockedEngineType;
            SyncEngineSelectorsFromDefinition(requestedType);
            if (_engineDesignerFamily == EngineDesignerFamily.Piston)
            {
                GUILayout.Label($"Cylinder Arrangement (Locked): {_selectedPistonArrangement}");
            }
            else
            {
                GUILayout.Label($"Turbine Type (Locked): {_selectedJetTurbineType}");
            }
        }
        else
        {
            _engineDesignerFamily = DrawEngineFamilySelector("Engine Type", _engineDesignerFamily);
            requestedType = _engineDesignerFamily == EngineDesignerFamily.Piston
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
        }

        if (!TryResolveEngineDefinitionForType(requestedType, out var selectedDefinitionIndex))
        {
            DrawError($"No engine definitions found for {requestedType} in {_currentDate.Year}. Add one in engine-definitions.json.");
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
            if (isVersionDevelopment)
            {
                _engineParameters.CylinderCount = Mathf.Clamp(
                    activeEngineVersionDevelopment!.LockedCylinderCount,
                    EngineDesignerBalance.Engine.PistonCylinderMin,
                    maxPistonCylinders);
                GUILayout.Label($"Number Of Cylinders: {FormatUiInteger(_engineParameters.CylinderCount)} (Locked)");
            }
            else
            {
                _engineParameters.CylinderCount = DrawIntSlider(
                    "Number Of Cylinders",
                    _engineParameters.CylinderCount,
                    EngineDesignerBalance.Engine.PistonCylinderMin,
                    maxPistonCylinders);
            }
            _engineParameters.CompressionRatio = DrawSlider(
                "Compression Ratio",
                _engineParameters.CompressionRatio,
                EngineDesignerBalance.Engine.PistonCompressionMin,
                EngineDesignerBalance.Engine.PistonCompressionMax,
                "0.00",
                PistonCompressionTooltip);
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
                "0.0",
                useLinearScale: true);
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
                "0.0",
                JetCompressionTooltip);
            _engineParameters.MaximumSpeed = DrawSlider(
                "Maximum Speed (RPM)",
                _engineParameters.MaximumSpeed,
                EngineDesignerBalance.Engine.JetSpeedMinRpm,
                EngineDesignerBalance.Engine.JetSpeedMaxRpm,
                "0");
            _engineParameters.HasAfterburner = DrawModifierToggleWithTooltip(
                _engineParameters.HasAfterburner,
                "Afterburner",
                BuildAfterburnerTooltipDefinition());
            _engineParameters.BoostType = BoostType.None;
            _engineParameters.EfficiencyOptimizationsPercent = EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent;
        }

        _engineParameters.CoolingEfficiency = DrawSlider(
            "Cooling Efficiency",
            _engineParameters.CoolingEfficiency,
            0d,
            1d,
            "0.00",
            useLinearScale: true);

        RebuildEnginePreview();

        GUILayout.BeginHorizontal();
        var saveButtonLabel = isVersionDevelopment ? "Save New Engine Version" : "Save Engine";
        if (GUILayout.Button(saveButtonLabel, GUILayout.Height(30f)))
        {
            SaveCurrentEngine();
        }

        if (GUILayout.Button("Reset Parameters", GUILayout.Height(30f)))
        {
            _engineParameters = isVersionDevelopment
                ? activeEngineVersionDevelopment!.SourceParameters.Clone()
                : CreateDefaultEngineParameters(_engineDefinitions[_selectedEngineDefinitionIndex]);
            _enginePreviewError = string.Empty;
            RebuildEnginePreview();
        }

        var compareEngineButtonLabel = _engineCompareReferenceIndex >= 0
            ? "Stop Compare"
            : "Compare Version";
        if (GUILayout.Button(compareEngineButtonLabel, GUILayout.Height(30f)))
        {
            ToggleEngineCompareMode();
        }

        GUILayout.EndHorizontal();

        if (_engineCompareReferenceIndex >= 0)
        {
            if (_savedEngines.Count == 0)
            {
                _engineCompareReferenceIndex = -1;
            }
            else
            {
                DrawSection("Compare Version");
                var compareLabels = _savedEngines
                    .Select(saved => BuildEngineDisplayName(saved.Name, saved.Definition.EngineType, saved.Parameters))
                    .ToArray();
                _engineCompareReferenceIndex = DrawListSelector("Reference Engine", _engineCompareReferenceIndex, compareLabels);
            }
        }

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
                _engineCompareReferenceIndex = -1;
            }
            else if (_selectedSavedEngineIndex >= _savedEngines.Count)
            {
                _selectedSavedEngineIndex = _savedEngines.Count - 1;
            }

            if (_engineCompareReferenceIndex == toDelete)
            {
                _engineCompareReferenceIndex = -1;
            }
            else if (_engineCompareReferenceIndex > toDelete)
            {
                _engineCompareReferenceIndex--;
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
        var fuelMetricLabel = isJet ? "L/N" : "L/kWh";

        var originalEngine = _enginePreview;
        var hasInlineComparison = TryGetEngineReferenceForInlineComparison(out originalEngine, out var originalReferenceLabel);
        var originalReport = originalEngine.Performance;
        var originalIsJet = hasInlineComparison && IsJetFamily(originalEngine.Definition.EngineType);
        var originalOutputLabel = originalReport.OutputKind == EngineOutputKind.PowerKilowatts ? "Power (kW)" : "Thrust (N)";
        var originalFuelMetricLabel = originalIsJet ? "L/N" : "L/kWh";

        DrawSection("Engine Visual");
        var engineVisualRect = GUILayoutUtility.GetRect(110f, 230f, GUILayout.ExpandWidth(true));
        EnginePreviewPainter.Draw(engineVisualRect, enginePreviewKind);
        GUILayout.Label($"Visual Type: {EnginePreviewPainter.GetLabel(enginePreviewKind)}");

        GUILayout.Label($"Definition: {_enginePreview.Definition.DisplayName} ({_enginePreview.Definition.EngineType})");
        if (hasInlineComparison)
        {
            GUILayout.Label($"Original Reference: {originalReferenceLabel}");
        }

        if (isJet)
        {
            DrawInlineComparisonRow(
                "Turbine Diameter (cm)",
                FormatUiNumber(_enginePreview.Parameters.TurbineDiameterMeters * 100d, "0"),
                hasInlineComparison,
                originalIsJet
                    ? FormatUiNumber(originalEngine.Parameters.TurbineDiameterMeters * 100d, "0")
                    : "N/A");
        }
        else
        {
            DrawInlineComparisonRow(
                "Cylinder Count",
                FormatUiInteger(_enginePreview.Parameters.CylinderCount),
                hasInlineComparison,
                originalIsJet
                    ? "N/A"
                    : FormatUiInteger(originalEngine.Parameters.CylinderCount));
            DrawInlineComparisonRow(
                "Optimization Height (m)",
                FormatUiNumber(_enginePreview.Parameters.OptimizationAltitudeMeters, "0"),
                hasInlineComparison,
                originalIsJet
                    ? "N/A"
                    : FormatUiNumber(originalEngine.Parameters.OptimizationAltitudeMeters, "0"));
        }

        foreach (var p in report.AltitudePerformance)
        {
            var fuelPerOutput = CalculateFuelPerOutputMetric(
                p.Output,
                p.FuelConsumptionKilogramsPerHour,
                isJet);
            var originalOutput = 0d;
            var originalFuelBurn = 0d;

            var hasOriginalAltitude = hasInlineComparison
                && TryGetEngineAltitudePerformanceValues(
                    originalEngine,
                    p.AltitudeMeters,
                    out originalOutput,
                    out originalFuelBurn);

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"{FormatUiNumber(p.AltitudeMeters, "0")}m", GUILayout.Width(70f));
            GUILayout.Label($"{outputLabel}: {FormatUiNumber(p.Output, "0.0")}", GUILayout.Width(170f));
            GUILayout.Label($"Fuel: {FormatUiNumber(p.FuelConsumptionKilogramsPerHour, "0.0")} kg/h", GUILayout.Width(160f));
            GUILayout.Label($"{fuelMetricLabel}: {FormatUiNumber(fuelPerOutput, "0.0000")}", GUILayout.Width(190f));
            if (hasOriginalAltitude)
            {
                var originalFuelPerOutput = CalculateFuelPerOutputMetric(originalOutput, originalFuelBurn, originalIsJet);
                GUILayout.Label(
                    $"Original -> {originalOutputLabel}: {FormatUiNumber(originalOutput, "0.0")} | Fuel: {FormatUiNumber(originalFuelBurn, "0.0")} kg/h | {originalFuelMetricLabel}: {FormatUiNumber(originalFuelPerOutput, "0.0000")}");
            }
            GUILayout.EndHorizontal();
        }

        DrawInlineComparisonRow(
            "Reliability",
            FormatUiNumber(report.Reliability, "0.000"),
            hasInlineComparison,
            FormatUiNumber(originalReport.Reliability, "0.000"));
        DrawInlineComparisonRow(
            "Weight (kg)",
            FormatUiNumber(report.Weight.Kilograms, "0.0"),
            hasInlineComparison,
            FormatUiNumber(originalReport.Weight.Kilograms, "0.0"));
        DrawInlineComparisonRow(
            "Cost",
            FormatUiNumber(report.Cost, "0"),
            hasInlineComparison,
            FormatUiNumber(originalReport.Cost, "0"));
        DrawInlineComparisonRow(
            "Production MD",
            FormatUiNumber(report.ProductionManDays, "0.0"),
            hasInlineComparison,
            FormatUiNumber(originalReport.ProductionManDays, "0.0"));
        var currentEngineResearchTimeMonths = ResolveEngineResearchTimeMonthsForDesignerPreview(
            _enginePreview,
            _engineVersionDevelopment?.SourceDevelopmentStartedYear);
        var originalEngineResearchTimeMonths = hasInlineComparison
            ? ResolveEngineResearchTimeMonthsForDesignerPreview(originalEngine)
            : currentEngineResearchTimeMonths;
        DrawInlineComparisonRow(
            "Engineering Time (months)",
            FormatUiInteger(currentEngineResearchTimeMonths),
            hasInlineComparison,
            FormatUiInteger(originalEngineResearchTimeMonths));

        DrawSection("Effect Summary");
        GUILayout.Label("Higher compression/boost gives more output but higher fuel burn and lower reliability.");
        GUILayout.Label("Turbine diameter and bypass ratio strongly affect jet-family engines.");
        GUILayout.Label("Higher cooling improves durability but increases mass and trims peak output.");

        DrawEngineComparisonOnRightPanel();
    }

    private void DrawAirframeTab()
    {
        DrawSection("Airframe + Family/Type (M3 + M4)");
        var activeTypeVersionDevelopment = _typeVersionDevelopment;
        var isTypeVersionDevelopment = activeTypeVersionDevelopment is not null;

        if (_savedEngines.Count == 0)
        {
            DrawError("No saved engine. Save an engine in the first tab.");
            if (GUILayout.Button("Go To Engine Tab", GUILayout.Height(30f)))
            {
                _tab = 0;
            }

            return;
        }

        var compositeMaterialUnlocked = IsCompositeMaterialUnlockedForCurrentState();
        if (!compositeMaterialUnlocked && _lockedMaterial == MaterialType.Composite)
        {
            _lockedMaterial = MaterialType.Aluminum;
            _baseAirframeParameters.MaterialType = _lockedMaterial;
        }

        if (isTypeVersionDevelopment)
        {
            DrawSection("Developing New Type Version");
            GUILayout.Label(
                $"Base type: {activeTypeVersionDevelopment!.SourceDisplayName} | " +
                $"family: {activeTypeVersionDevelopment.SourceFamilyDisplayName} | " +
                $"development start: {FormatUiInteger(activeTypeVersionDevelopment.SourceDevelopmentStartedYear)} | " +
                $"variant start: {FormatUiInteger(activeTypeVersionDevelopment.VariantDevelopmentStartedYear)}");
            GUILayout.Label("Family and assigned engine are fixed while developing this type version.");

            if (GUILayout.Button("Cancel Version Development", GUILayout.Height(26f)))
            {
                _typeVersionDevelopment = null;
            }
        }

        DrawSection("Family");
        if (isTypeVersionDevelopment)
        {
            GUILayout.Label($"Family Name (locked): {activeTypeVersionDevelopment!.SourceFamilyDisplayName}");
            GUILayout.Label($"Family ID (locked): {activeTypeVersionDevelopment.SourceFamilyId}");
            GUILayout.Label($"Engines: {FormatUiInteger(_lockedEngineCount)} | Structure: {_lockedStructure} | Material: {_lockedMaterial}");
            GUILayout.Label($"Allowed Engine Family: {_familyAllowedEngineFamily}");
        }
        else
        {
            _familyName = DrawText("Family Name", _familyName);
            GUILayout.Label($"Family ID (auto): {BuildFamilyId()}");
            _lockedEngineCount = DrawIntSlider("Number Of Engines", _lockedEngineCount, 1, 6, useLinearScale: true);
            _lockedStructure = DrawEnum("Structure Type", _lockedStructure, BuildStructureTooltipDefinition(_lockedStructure));
            _lockedMaterial = DrawMaterialSelector("Material", _lockedMaterial, compositeMaterialUnlocked);
            if (!compositeMaterialUnlocked)
            {
                GUILayout.Label(
                    $"Composite material unlocks after event '{PlaneDesignerBalance.Airframe.CompositeMaterialUnlockEventId}' " +
                    $"({FormatUiInteger(PlaneDesignerBalance.Airframe.CompositeMaterialFallbackUnlockYear)}).");
            }
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
        }

        DrawSection("Type");
        IReadOnlyList<AircraftCategory> availableAircraftCategories = Array.Empty<AircraftCategory>();
        if (!isTypeVersionDevelopment)
        {
            NormalizeSelectedAircraftCategoryForHistoricalCampaign();
            availableAircraftCategories = GetUnlockedAircraftCategoriesForCurrentState();
        }
        _typeName = BuildTypeDisplayName();
        GUILayout.Label($"Type Name (auto): {_typeName}");
        GUILayout.Label($"Type ID (auto): {BuildTypeId()}");
        if (isTypeVersionDevelopment)
        {
            var categoryProfile = AircraftCategoryCatalog.GetProfile(_selectedAircraftCategory);
            GUILayout.Label($"Aircraft Category (locked): {categoryProfile.DisplayName}");
        }
        else
        {
            _selectedAircraftCategory = DrawAircraftCategorySelector(
                "Aircraft Category",
                _selectedAircraftCategory,
                availableAircraftCategories);
        }
        _typeCrewCount = DrawIntSlider(
            "Crew Members",
            _typeCrewCount,
            PlaneDesignerBalance.Airframe.CrewMin,
            PlaneDesignerBalance.Airframe.CrewMax);
        _typeTailType = DrawTailSelector("Tail Type", _typeTailType);
        _typeWingShape = DrawEnum("Wing Shape", _typeWingShape, BuildWingShapeTooltipDefinition(_typeWingShape));
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
        _typeWingAreaSquareMeters = DrawWingAreaSliderWithOptimizationButton(
            "Wing Area (m2)",
            _typeWingAreaSquareMeters,
            TypeWingAreaMinSquareMeters,
            TypeWingAreaMaxSquareMeters,
            "0.0");

        DrawSection("Type Engine");
        if (isTypeVersionDevelopment)
        {
            var sourceEngineIndex = FindSavedEngineIndexByResearchId(activeTypeVersionDevelopment!.SourceEngineResearchId);
            if (sourceEngineIndex >= 0)
            {
                _selectedSavedEngineIndex = sourceEngineIndex;
                var sourceEngine = _savedEngines[sourceEngineIndex];
                GUILayout.Label(
                    $"Assigned Engine (locked): {BuildEngineDisplayName(sourceEngine.Name, sourceEngine.Definition.EngineType, sourceEngine.Parameters)}");
            }
            else
            {
                DrawError("Source engine for this type version is missing.");
            }
        }
        else
        {
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
        }

        _typeFlapType = DrawTrailingFlapSelector("Flaps", _typeFlapType);
        _typeHasLeadingEdgeFlaps = DrawModifierToggleWithTooltip(
            _typeHasLeadingEdgeFlaps,
            "Leading Edge Flaps",
            BuildLeadingEdgeFlapsTooltipDefinition());
        _typeHasBackupSystems = DrawModifierToggleWithTooltip(
            _typeHasBackupSystems,
            "Backup Systems (+0.2 reliability, +10% weight, +3% drag)",
            BuildBackupSystemsTooltipDefinition());
        _typeLandingGearType = DrawLandingGearSelector("Gear Type", _typeLandingGearType);
        _typeRunwayType = DrawRunwayTypeSelector("Runway Type", _typeRunwayType);
        _typeCowling = DrawSlider("Cowling", _typeCowling, 0.1d, 0.9d, "0.00", useLinearScale: true);
        _typeAerodynamicCleanlinessPercent = DrawSlider(
            "Aerodynamic Cleanliness (%)",
            _typeAerodynamicCleanlinessPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMinPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent,
            "0.0",
            useLinearScale: true);
        _typeFuelLiters = DrawSlider(
            "Fuel (L)",
            _typeFuelLiters,
            PlaneDesignerBalance.Airframe.FuelMinLiters,
            PlaneDesignerBalance.Airframe.FuelMaxLiters,
            "0");
        if (isTypeVersionDevelopment)
        {
            DrawTypeVersionComparison(activeTypeVersionDevelopment!);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Type", GUILayout.Height(28f)))
        {
            var sourceTypeIndex = isTypeVersionDevelopment
                ? FindSavedAircraftDesignIndexByTypeResearchId(activeTypeVersionDevelopment!.SourceTypeResearchId)
                : -1;
            if (isTypeVersionDevelopment)
            {
                if (sourceTypeIndex >= 0
                    && TryLoadSavedAircraftDesignIntoTypeDesigner(sourceTypeIndex, out _, out _))
                {
                    _aircraftPreviewError = string.Empty;
                }
                else
                {
                    _aircraftPreviewError = "Source type for this version is missing.";
                }
            }
            else
            {
                SyncTypeAirframeFromBase();
            }
        }

        var compareTypeButtonLabel = _typeCompareReferenceIndex >= 0
            ? "Stop Compare"
            : "Compare Version";
        if (GUILayout.Button(compareTypeButtonLabel, GUILayout.Height(28f)))
        {
            ToggleTypeCompareMode();
        }

        GUILayout.EndHorizontal();

        if (_typeCompareReferenceIndex >= 0)
        {
            if (_savedAircraftDesigns.Count == 0)
            {
                _typeCompareReferenceIndex = -1;
            }
            else
            {
                DrawSection("Compare Version");
                var compareLabels = _savedAircraftDesigns
                    .Select(design =>
                        $"{design.Name} | " +
                        $"{AircraftCategoryCatalog.GetProfile(design.CatalogItem.Category).DisplayName} | " +
                        $"{ResolveSavedEngineDisplayNameForAircraftDesign(design)}")
                    .ToArray();
                _typeCompareReferenceIndex = DrawListSelector("Reference Variant", _typeCompareReferenceIndex, compareLabels);
            }
        }

        _aircraftDesignName = DrawText("Saved Aircraft Name", _aircraftDesignName);

        var saveTypeButtonLabel = isTypeVersionDevelopment ? "Save Type Version" : "Save Aircraft Design";
        if (GUILayout.Button(saveTypeButtonLabel, GUILayout.Height(30f)))
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
                    $"{design.Name} | {AircraftCategoryCatalog.GetProfile(design.CatalogItem.Category).DisplayName} | {ResolveSavedEngineDisplayNameForAircraftDesign(design)} x{FormatUiInteger(design.CatalogItem.Aircraft.Airframe.Parameters.EngineCount)} | {GetAircraftDesignResearchStatus(design)}")
                .ToArray();
            _selectedSavedAircraftDesignIndex = DrawListSelector("Select Saved Design", _selectedSavedAircraftDesignIndex < 0 ? 0 : _selectedSavedAircraftDesignIndex, labels);

            if (_selectedSavedAircraftDesignIndex >= 0 && _selectedSavedAircraftDesignIndex < _savedAircraftDesigns.Count)
            {
                if (GUILayout.Button("Delete Selected Aircraft Design", GUILayout.Height(26f)))
                {
                    var deletedIndex = _selectedSavedAircraftDesignIndex;
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

                        var contractIds = _excludedWarehouseDesignByContractId.Keys.ToArray();
                        for (var contractIndex = 0; contractIndex < contractIds.Length; contractIndex++)
                        {
                            var contractId = contractIds[contractIndex];
                            if (!_excludedWarehouseDesignByContractId.TryGetValue(contractId, out var excludedDesignIds))
                            {
                                continue;
                            }

                            excludedDesignIds.Remove(designToDelete.CatalogItem.DesignId);
                            if (excludedDesignIds.Count == 0)
                            {
                                _excludedWarehouseDesignByContractId.Remove(contractId);
                            }
                        }

                        _factoryScrapQuantityInputByDesignId.Remove(designToDelete.CatalogItem.DesignId);

                        if (_savedAircraftDesigns.Count == 0)
                        {
                            _selectedSavedAircraftDesignIndex = -1;
                            _typeCompareReferenceIndex = -1;
                        }
                        else if (_selectedSavedAircraftDesignIndex >= _savedAircraftDesigns.Count)
                        {
                            _selectedSavedAircraftDesignIndex = _savedAircraftDesigns.Count - 1;
                        }

                        if (_typeCompareReferenceIndex == deletedIndex)
                        {
                            _typeCompareReferenceIndex = -1;
                        }
                        else if (_typeCompareReferenceIndex > deletedIndex)
                        {
                            _typeCompareReferenceIndex--;
                        }
                    }
                }
            }
        }

        RebuildAircraftPreview();
        if (_aircraftPreview is not null)
        {
            DrawSection("Live Category Control");
            var liveCategoryProfile = AircraftCategoryCatalog.GetProfileForYear(_aircraftPreview.FamilyDefinition.Category, _currentDate.Year);
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

        var resetFamilyAndTypeLabel = isTypeVersionDevelopment ? "Reset Type To Source" : "Reset Family + Type Config";
        if (GUILayout.Button(resetFamilyAndTypeLabel, GUILayout.Height(30f)))
        {
            var sourceTypeIndex = isTypeVersionDevelopment
                ? FindSavedAircraftDesignIndexByTypeResearchId(activeTypeVersionDevelopment!.SourceTypeResearchId)
                : -1;
            if (isTypeVersionDevelopment)
            {
                if (sourceTypeIndex >= 0
                    && TryLoadSavedAircraftDesignIntoTypeDesigner(sourceTypeIndex, out _, out _))
                {
                    _aircraftPreviewError = string.Empty;
                }
                else
                {
                    _aircraftPreviewError = "Source type for this version is missing.";
                }
            }
            else
            {
                ResetFamilyConfig();
                SyncTypeAirframeFromBase();
            }
        }
    }

    private void DrawEngineVersionComparison(
        EngineParameters originalParameters,
        EngineParameters newParameters,
        EngineType engineType)
    {
        DrawSection("Original vs New Engine Parameters");
        DrawEngineParameterComparisonRows(engineType, originalParameters, engineType, newParameters);
    }

    private void DrawTypeVersionComparison(TypeVersionDevelopmentContext context)
    {
        if (!TryGetSavedAircraftDesignByTypeResearchId(context.SourceTypeResearchId, out var sourceDesign))
        {
            DrawError("Original type parameters are unavailable for comparison.");
            return;
        }

        var sourceAirframe = sourceDesign.CatalogItem.Aircraft.Airframe.Parameters;
        var originalCategory = AircraftCategoryCatalog.GetProfile(sourceDesign.CatalogItem.Category).DisplayName;
        var currentCategory = AircraftCategoryCatalog.GetProfile(_selectedAircraftCategory).DisplayName;

        var originalEngineLabel = ResolveSavedEngineDisplayNameForAircraftDesign(sourceDesign);
        var currentEngineLabel = originalEngineLabel;
        if (_selectedSavedEngineIndex >= 0 && _selectedSavedEngineIndex < _savedEngines.Count)
        {
            var currentEngine = _savedEngines[_selectedSavedEngineIndex];
            currentEngineLabel = BuildEngineDisplayName(
                currentEngine.Name,
                currentEngine.Definition.EngineType,
                currentEngine.Parameters);
        }

        DrawSection("Original vs New Type Parameters");
        DrawTypeParameterComparisonRows(
            sourceAirframe,
            BuildCurrentTypeAirframeParametersForComparison(),
            originalCategory,
            currentCategory,
            originalEngineLabel,
            currentEngineLabel);
    }

    private static void DrawVersionComparisonRow(string label, string originalValue, string newValue)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(220f));
        GUILayout.Label($"Original: {originalValue}", GUILayout.Width(280f));
        GUILayout.Label($"New: {newValue}");
        GUILayout.EndHorizontal();
    }

    private static void DrawParameterValueRow(string label, string value)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(220f));
        GUILayout.Label(value);
        GUILayout.EndHorizontal();
    }

    private static void DrawInlineComparisonRow(
        string label,
        string newValue,
        bool hasOriginal,
        string originalValue)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(240f));
        GUILayout.Label(newValue, GUILayout.Width(180f));
        if (hasOriginal)
        {
            GUILayout.Label($"Original: {originalValue}");
        }

        GUILayout.EndHorizontal();
    }

    private bool TryGetEngineReferenceForInlineComparison(out Engine referenceEngine, out string referenceLabel)
    {
        referenceEngine = _enginePreview!;
        referenceLabel = string.Empty;
        if (_engineCompareReferenceIndex >= 0 && _engineCompareReferenceIndex < _savedEngines.Count)
        {
            var savedReference = _savedEngines[_engineCompareReferenceIndex];
            try
            {
                referenceEngine = BuildSavedEngineAtReferenceDate(savedReference);
                referenceLabel = BuildEngineDisplayName(
                    savedReference.Name,
                    savedReference.Definition.EngineType,
                    savedReference.Parameters);
                return true;
            }
            catch
            {
                // Keep searching for fallback source reference.
            }
        }

        if (_engineVersionDevelopment is not null
            && TryBuildSavedEngineSnapshot(_engineVersionDevelopment.SourceResearchId, out var sourceEngine))
        {
            referenceEngine = sourceEngine;
            referenceLabel = _engineVersionDevelopment.SourceDisplayName;
            return true;
        }

        return false;
    }

    private void DrawEngineComparisonOnRightPanel()
    {
        if (_enginePreview is null)
        {
            return;
        }

        if (_engineVersionDevelopment is not null)
        {
            DrawSection("Compared Version (Source)");
            GUILayout.Label($"Source: {_engineVersionDevelopment.SourceDisplayName}");
            DrawEngineParameterComparisonRows(
                _engineVersionDevelopment.LockedEngineType,
                _engineVersionDevelopment.SourceParameters,
                _enginePreview.Definition.EngineType,
                _enginePreview.Parameters);
        }

        if (_engineCompareReferenceIndex < 0 || _engineCompareReferenceIndex >= _savedEngines.Count)
        {
            return;
        }

        var reference = _savedEngines[_engineCompareReferenceIndex];
        DrawSection("Compared Version (Selected)");
        GUILayout.Label($"Reference: {BuildEngineDisplayName(reference.Name, reference.Definition.EngineType, reference.Parameters)}");
        DrawEngineParameterComparisonRows(
            reference.Definition.EngineType,
            reference.Parameters,
            _enginePreview.Definition.EngineType,
            _enginePreview.Parameters);

        try
        {
            var referenceEngine = BuildSavedEngineAtReferenceDate(reference);
            DrawEnginePerformanceComparisonRows(referenceEngine, _enginePreview);
        }
        catch
        {
            DrawError("Selected engine could not be built for performance comparison.");
        }
    }

    private void DrawTypeComparisonOnRightPanel()
    {
        if (_aircraftPreview is null)
        {
            return;
        }

        if (_typeVersionDevelopment is not null
            && TryGetSavedAircraftDesignByTypeResearchId(_typeVersionDevelopment.SourceTypeResearchId, out var sourceDesign))
        {
            DrawSection("Compared Version (Source)");
            GUILayout.Label($"Source: {_typeVersionDevelopment.SourceDisplayName}");
            DrawTypeParameterComparisonRows(
                sourceDesign.CatalogItem.Aircraft.Airframe.Parameters,
                _aircraftPreview.Airframe.Parameters,
                AircraftCategoryCatalog.GetProfile(sourceDesign.CatalogItem.Category).DisplayName,
                AircraftCategoryCatalog.GetProfile(_aircraftPreview.FamilyDefinition.Category).DisplayName,
                ResolveSavedEngineDisplayNameForAircraftDesign(sourceDesign),
                BuildEngineDisplayName(
                    _aircraftPreview.Engine.Definition.DisplayName,
                    _aircraftPreview.Engine.Definition.EngineType,
                    _aircraftPreview.Engine.Parameters));
            DrawTypePerformanceComparisonRows(sourceDesign.CatalogItem.Aircraft, _aircraftPreview);
        }

        if (_typeCompareReferenceIndex < 0 || _typeCompareReferenceIndex >= _savedAircraftDesigns.Count)
        {
            return;
        }

        var reference = _savedAircraftDesigns[_typeCompareReferenceIndex];
        var referenceAircraft = reference.CatalogItem.Aircraft;
        DrawSection("Compared Version (Selected)");
        GUILayout.Label($"Reference: {reference.Name}");
        DrawTypeParameterComparisonRows(
            referenceAircraft.Airframe.Parameters,
            _aircraftPreview.Airframe.Parameters,
            AircraftCategoryCatalog.GetProfile(reference.CatalogItem.Category).DisplayName,
            AircraftCategoryCatalog.GetProfile(_aircraftPreview.FamilyDefinition.Category).DisplayName,
            ResolveSavedEngineDisplayNameForAircraftDesign(reference),
            BuildEngineDisplayName(
                _aircraftPreview.Engine.Definition.DisplayName,
                _aircraftPreview.Engine.Definition.EngineType,
                _aircraftPreview.Engine.Parameters));
        DrawTypePerformanceComparisonRows(referenceAircraft, _aircraftPreview);
    }

    private static void DrawEngineParameterComparisonRows(
        EngineType originalEngineType,
        EngineParameters originalParameters,
        EngineType newEngineType,
        EngineParameters newParameters)
    {
        DrawVersionComparisonRow("Engine Type", originalEngineType.ToString(), newEngineType.ToString());
        DrawVersionComparisonRow(
            "Cylinder Count",
            IsJetFamily(originalEngineType) ? "N/A" : FormatUiInteger(originalParameters.CylinderCount),
            IsJetFamily(newEngineType) ? "N/A" : FormatUiInteger(newParameters.CylinderCount));
        DrawVersionComparisonRow(
            "Turbine Diameter (cm)",
            IsJetFamily(originalEngineType) ? FormatUiNumber(originalParameters.TurbineDiameterMeters * 100d, "0") : "N/A",
            IsJetFamily(newEngineType) ? FormatUiNumber(newParameters.TurbineDiameterMeters * 100d, "0") : "N/A");
        DrawVersionComparisonRow(
            "Displacement (L)",
            IsJetFamily(originalEngineType) ? "N/A" : FormatUiNumber(originalParameters.DisplacementLiters, "0.0"),
            IsJetFamily(newEngineType) ? "N/A" : FormatUiNumber(newParameters.DisplacementLiters, "0.0"));
        DrawVersionComparisonRow(
            "Optimization Height (m)",
            IsJetFamily(originalEngineType) ? "N/A" : FormatUiNumber(originalParameters.OptimizationAltitudeMeters, "0"),
            IsJetFamily(newEngineType) ? "N/A" : FormatUiNumber(newParameters.OptimizationAltitudeMeters, "0"));
        DrawVersionComparisonRow(
            "Cooling Efficiency",
            FormatUiNumber(originalParameters.CoolingEfficiency, "0.00"),
            FormatUiNumber(newParameters.CoolingEfficiency, "0.00"));
    }

    private static void DrawEnginePerformanceComparisonRows(Engine originalEngine, Engine newEngine)
    {
        var originalIsJet = IsJetFamily(originalEngine.Definition.EngineType);
        var newIsJet = IsJetFamily(newEngine.Definition.EngineType);
        var originalOutputUnit = originalEngine.Performance.OutputKind == EngineOutputKind.PowerKilowatts ? "kW" : "N";
        var newOutputUnit = newEngine.Performance.OutputKind == EngineOutputKind.PowerKilowatts ? "kW" : "N";
        var originalFuelMetric = originalIsJet ? "L/N" : "L/kWh";
        var newFuelMetric = newIsJet ? "L/N" : "L/kWh";

        var altitudes = originalEngine.Performance.AltitudePerformance
            .Select(entry => entry.AltitudeMeters)
            .Concat(newEngine.Performance.AltitudePerformance.Select(entry => entry.AltitudeMeters))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        for (var altitudeIndex = 0; altitudeIndex < altitudes.Length; altitudeIndex++)
        {
            var altitude = altitudes[altitudeIndex];
            var altitudeLabel = $"{FormatUiNumber(altitude, "0")}m";

            var hasOriginal = TryGetEngineAltitudePerformanceValues(originalEngine, altitude, out var originalOutput, out var originalFuelBurn);
            var hasNew = TryGetEngineAltitudePerformanceValues(newEngine, altitude, out var newOutput, out var newFuelBurn);

            DrawVersionComparisonRow(
                $"{altitudeLabel} Output",
                hasOriginal ? $"{FormatUiNumber(originalOutput, "0.0")} {originalOutputUnit}" : "N/A",
                hasNew ? $"{FormatUiNumber(newOutput, "0.0")} {newOutputUnit}" : "N/A");
            DrawVersionComparisonRow(
                $"{altitudeLabel} Fuel (kg/h)",
                hasOriginal ? FormatUiNumber(originalFuelBurn, "0.0") : "N/A",
                hasNew ? FormatUiNumber(newFuelBurn, "0.0") : "N/A");
            DrawVersionComparisonRow(
                $"{altitudeLabel} Fuel Metric",
                hasOriginal ? $"{FormatUiNumber(CalculateFuelPerOutputMetric(originalOutput, originalFuelBurn, originalIsJet), "0.0000")} {originalFuelMetric}" : "N/A",
                hasNew ? $"{FormatUiNumber(CalculateFuelPerOutputMetric(newOutput, newFuelBurn, newIsJet), "0.0000")} {newFuelMetric}" : "N/A");
        }

        DrawVersionComparisonRow(
            "Reliability",
            FormatUiNumber(originalEngine.Performance.Reliability, "0.000"),
            FormatUiNumber(newEngine.Performance.Reliability, "0.000"));
        DrawVersionComparisonRow(
            "Weight (kg)",
            FormatUiNumber(originalEngine.Performance.Weight.Kilograms, "0.0"),
            FormatUiNumber(newEngine.Performance.Weight.Kilograms, "0.0"));
        DrawVersionComparisonRow(
            "Cost",
            FormatUiNumber(originalEngine.Performance.Cost, "0"),
            FormatUiNumber(newEngine.Performance.Cost, "0"));
        DrawVersionComparisonRow(
            "Production MD",
            FormatUiNumber(originalEngine.Performance.ProductionManDays, "0.0"),
            FormatUiNumber(newEngine.Performance.ProductionManDays, "0.0"));
    }

    private static bool TryGetEngineAltitudePerformanceValues(
        Engine engine,
        double altitudeMeters,
        out double output,
        out double fuelBurnKilogramsPerHour)
    {
        var entries = engine.Performance.AltitudePerformance;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (System.Math.Abs(entry.AltitudeMeters - altitudeMeters) > 0.1d)
            {
                continue;
            }

            output = entry.Output;
            fuelBurnKilogramsPerHour = entry.FuelConsumptionKilogramsPerHour;
            return true;
        }

        output = 0d;
        fuelBurnKilogramsPerHour = 0d;
        return false;
    }

    private static double ResolveEngineOutputAtSeaLevel(Engine engine)
    {
        if (engine is null)
        {
            return 0d;
        }

        if (TryGetEngineAltitudePerformanceValues(engine, 0d, out var seaLevelOutput, out _))
        {
            return seaLevelOutput;
        }

        var entries = engine.Performance.AltitudePerformance;
        return entries.Count == 0
            ? 0d
            : entries[0].Output;
    }

    private static void DrawTypeParameterComparisonRows(
        AirframeParameters originalAirframe,
        AirframeParameters newAirframe,
        string originalCategory,
        string newCategory,
        string originalEngineLabel,
        string newEngineLabel)
    {
        DrawVersionComparisonRow("Aircraft Category", originalCategory, newCategory);
        DrawVersionComparisonRow("Assigned Engine", originalEngineLabel, newEngineLabel);
        DrawVersionComparisonRow(
            "Crew Members",
            FormatUiInteger(originalAirframe.CrewCount),
            FormatUiInteger(newAirframe.CrewCount));
        DrawVersionComparisonRow(
            "Wing Area (m2)",
            FormatUiNumber(originalAirframe.WingAreaSquareMeters, "0.0"),
            FormatUiNumber(newAirframe.WingAreaSquareMeters, "0.0"));
        DrawVersionComparisonRow(
            "Landing Gear",
            originalAirframe.LandingGearType.ToString(),
            newAirframe.LandingGearType.ToString());
        DrawVersionComparisonRow(
            "Cowling",
            FormatUiNumber(originalAirframe.EngineCowling, "0.00"),
            FormatUiNumber(newAirframe.EngineCowling, "0.00"));
        DrawVersionComparisonRow(
            "Fuel (L)",
            FormatUiNumber(originalAirframe.FuelAmountLiters, "0"),
            FormatUiNumber(newAirframe.FuelAmountLiters, "0"));
    }

    private static void DrawTypePerformanceComparisonRows(Aircraft originalAircraft, Aircraft newAircraft)
    {
        var altitudes = originalAircraft.Performance.MaxSpeedByAltitude
            .Select(entry => entry.AltitudeMeters)
            .Concat(newAircraft.Performance.MaxSpeedByAltitude.Select(entry => entry.AltitudeMeters))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        for (var altitudeIndex = 0; altitudeIndex < altitudes.Length; altitudeIndex++)
        {
            var altitude = altitudes[altitudeIndex];
            var altitudeLabel = $"{FormatUiNumber(altitude, "0")}m";
            var hasOriginal = TryGetAircraftMaxSpeedAtAltitude(originalAircraft, altitude, out var originalMaxSpeedMetersPerSecond);
            var hasNew = TryGetAircraftMaxSpeedAtAltitude(newAircraft, altitude, out var newMaxSpeedMetersPerSecond);
            DrawVersionComparisonRow(
                $"Max Speed @ {altitudeLabel} (km/h)",
                hasOriginal ? FormatUiNumber(originalMaxSpeedMetersPerSecond * 3.6d, "0.0") : "N/A",
                hasNew ? FormatUiNumber(newMaxSpeedMetersPerSecond * 3.6d, "0.0") : "N/A");
        }

        var originalEngineOutputAtSeaLevel = ResolveEngineOutputAtSeaLevel(originalAircraft.Engine);
        var newEngineOutputAtSeaLevel = ResolveEngineOutputAtSeaLevel(newAircraft.Engine);
        var originalEngineCount = System.Math.Max(originalAircraft.Airframe.Parameters.EngineCount, 1);
        var newEngineCount = System.Math.Max(newAircraft.Airframe.Parameters.EngineCount, 1);
        var originalEngineOutputUnit = originalAircraft.Engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts ? "kW" : "N";
        var newEngineOutputUnit = newAircraft.Engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts ? "kW" : "N";
        var outputMetricLabel =
            originalAircraft.Engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts
            && newAircraft.Engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts
                ? "Power"
                : originalAircraft.Engine.Performance.OutputKind == EngineOutputKind.ThrustNewtons
                  && newAircraft.Engine.Performance.OutputKind == EngineOutputKind.ThrustNewtons
                    ? "Thrust"
                    : "Output";

        DrawVersionComparisonRow(
            $"Engine {outputMetricLabel} @0m",
            $"{FormatUiNumber(originalEngineOutputAtSeaLevel, "0.0")} {originalEngineOutputUnit}",
            $"{FormatUiNumber(newEngineOutputAtSeaLevel, "0.0")} {newEngineOutputUnit}");
        DrawVersionComparisonRow(
            $"Total {outputMetricLabel} @0m",
            $"{FormatUiNumber(originalEngineOutputAtSeaLevel * originalEngineCount, "0.0")} {originalEngineOutputUnit} ({FormatUiInteger(originalEngineCount)}x)",
            $"{FormatUiNumber(newEngineOutputAtSeaLevel * newEngineCount, "0.0")} {newEngineOutputUnit} ({FormatUiInteger(newEngineCount)}x)");

        DrawVersionComparisonRow(
            "Range (km)",
            FormatUiNumber(originalAircraft.Performance.RangeKilometers, "0.0"),
            FormatUiNumber(newAircraft.Performance.RangeKilometers, "0.0"));
        DrawVersionComparisonRow(
            "Optimal Cruise Speed (km/h)",
            FormatUiNumber(originalAircraft.Performance.OptimalCruiseSpeedMetersPerSecond * 3.6d, "0.0"),
            FormatUiNumber(newAircraft.Performance.OptimalCruiseSpeedMetersPerSecond * 3.6d, "0.0"));
        DrawVersionComparisonRow(
            "Cruise Fuel Burn Avg (kg/h)",
            FormatUiNumber(originalAircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour, "0.0"),
            FormatUiNumber(newAircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour, "0.0"));
        DrawVersionComparisonRow(
            "Useful Weight (kg)",
            FormatUiNumber(originalAircraft.Performance.PayloadCapacity.Kilograms, "0.0"),
            FormatUiNumber(newAircraft.Performance.PayloadCapacity.Kilograms, "0.0"));
        DrawVersionComparisonRow(
            "Maximum Takeoff Weight (kg)",
            FormatUiNumber(originalAircraft.Performance.MaximumTakeoffWeightKilograms, "0.0"),
            FormatUiNumber(newAircraft.Performance.MaximumTakeoffWeightKilograms, "0.0"));
        DrawVersionComparisonRow(
            "Empty Aircraft Weight (kg)",
            FormatUiNumber(originalAircraft.Performance.EmptyAircraftWeightKilograms, "0.0"),
            FormatUiNumber(newAircraft.Performance.EmptyAircraftWeightKilograms, "0.0"));
        DrawVersionComparisonRow(
            "Aircraft Weight With Fuel (kg)",
            FormatUiNumber(originalAircraft.Performance.AircraftWeightWithFuelKilograms, "0.0"),
            FormatUiNumber(newAircraft.Performance.AircraftWeightWithFuelKilograms, "0.0"));
        DrawVersionComparisonRow(
            "Overall Reliability",
            FormatUiNumber(originalAircraft.Performance.OverallReliability, "0.000"),
            FormatUiNumber(newAircraft.Performance.OverallReliability, "0.000"));
        DrawVersionComparisonRow(
            "Production Cost",
            FormatUiNumber(originalAircraft.Performance.TotalProductionCost, "0"),
            FormatUiNumber(newAircraft.Performance.TotalProductionCost, "0"));
        DrawVersionComparisonRow(
            "Production MD",
            FormatUiNumber(originalAircraft.Performance.TotalProductionManDays, "0.0"),
            FormatUiNumber(newAircraft.Performance.TotalProductionManDays, "0.0"));
    }

    private static bool TryGetAircraftMaxSpeedAtAltitude(
        Aircraft aircraft,
        double altitudeMeters,
        out double speedMetersPerSecond)
    {
        var entries = aircraft.Performance.MaxSpeedByAltitude;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (System.Math.Abs(entry.AltitudeMeters - altitudeMeters) > 0.1d)
            {
                continue;
            }

            speedMetersPerSecond = entry.MaxSpeedMetersPerSecond;
            return true;
        }

        speedMetersPerSecond = 0d;
        return false;
    }

    private AirframeParameters BuildCurrentTypeAirframeParametersForComparison()
    {
        var current = _baseAirframeParameters.Clone();
        current.CrewCount = _typeCrewCount;
        current.WingShape = _typeWingShape;
        current.WingProfile = _typeWingProfile;
        current.WingAreaSquareMeters = _typeWingAreaSquareMeters;
        current.TailType = _typeTailType;
        current.FlapType = _typeFlapType;
        current.HasLeadingEdgeFlaps = _typeHasLeadingEdgeFlaps;
        current.HasBackupSystems = _typeHasBackupSystems;
        current.LandingGearType = _typeLandingGearType;
        current.RunwayType = _typeRunwayType;
        current.EngineCowling = _typeCowling;
        current.AerodynamicCleanlinessPercent = _typeAerodynamicCleanlinessPercent;
        current.FuelAmountLiters = _typeFuelLiters;
        return current;
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
        var categoryProfile = AircraftCategoryCatalog.GetProfileForYear(_aircraftPreview.FamilyDefinition.Category, _currentDate.Year);

        GUILayout.Label($"Family: {_aircraftPreview.FamilyDefinition.DisplayName}");
        GUILayout.Label($"Category: {categoryProfile.DisplayName}");
        GUILayout.Label($"Type: {_aircraftPreview.TypeDefinition.DisplayName}");
        GUILayout.Label($"Engine: {_aircraftPreview.Engine.Definition.DisplayName} x{FormatUiInteger(_aircraftPreview.Airframe.Parameters.EngineCount)}");
        var engineOutputAtSeaLevel = ResolveEngineOutputAtSeaLevel(_aircraftPreview.Engine);
        var totalEngineOutputAtSeaLevel = engineOutputAtSeaLevel * System.Math.Max(_aircraftPreview.Airframe.Parameters.EngineCount, 1);
        if (_aircraftPreview.Engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts)
        {
            DrawMetric("Engine Power @0m (kW)", engineOutputAtSeaLevel, "0.0");
            DrawMetric("Total Power @0m (kW)", totalEngineOutputAtSeaLevel, "0.0");
        }
        else
        {
            DrawMetric("Engine Thrust @0m (N)", engineOutputAtSeaLevel, "0.0");
            DrawMetric("Total Thrust @0m (N)", totalEngineOutputAtSeaLevel, "0.0");
        }

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
        DrawMetric("Cruise Fuel Burn Avg (kg/h)", perf.AverageCruiseFuelBurnKilogramsPerHour, "0.0");
        DrawMetric("Useful Weight (kg)", perf.PayloadCapacity.Kilograms, "0.0");
        DrawMetric("Maximum Takeoff Weight (kg)", perf.MaximumTakeoffWeightKilograms, "0.0");
        DrawMetric("Aircraft Weight With Fuel (kg)", perf.AircraftWeightWithFuelKilograms, "0.0");
        DrawMetric("Empty Aircraft Weight (kg)", perf.EmptyAircraftWeightKilograms, "0.0");
        if (categoryProfile.HasPassengerCapacityRequirement)
        {
            DrawMetric($"Passengers (Payload/{FormatUiNumber(PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger, "0")}kg)", perf.PassengerCapacity, "0");
        }

        DrawMetric("Overall Reliability", perf.OverallReliability, "0.000");

        DrawMetric("Drag Coefficient (Cd)", airframePerf.DragCoefficient, "0.000");
        DrawMetric("Maneuverability", airframePerf.Maneuverability, "0.0");

        DrawMetric("Production Cost", perf.TotalProductionCost, "0");
        DrawMetric("Production MD", perf.TotalProductionManDays, "0.0");
        DrawMetric(
            "Engineering Time (months)",
            ResolveTypeResearchTimeMonthsForDesignerPreview(
                _aircraftPreview,
                _typeVersionDevelopment?.SourceDevelopmentStartedYear),
            "0");
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
        GUILayout.Label($"Runway options for this category: {FormatCategoryRunwayLengths(categoryProfile.Category)}");
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

        DrawTypeComparisonOnRightPanel();
    }

    private void DrawContractsTab()
    {
        DrawSection("Contracts (M6)");
        if (_contractBoard is null || _productionFactory is null)
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
        DrawContractRequestedCategoryFilter(
            "3) Desired Aircraft Type",
            ref _contractRequestedCategoryFilterEnabled,
            ref _contractRequestedCategoryFilter);
        _contractQuantityFilterMin = DrawIntSlider("4) Required Aircraft Min", _contractQuantityFilterMin, 5, 500);
        _contractQuantityFilterMax = DrawIntSlider("4) Required Aircraft Max", _contractQuantityFilterMax, 5, 500);
        if (_contractQuantityFilterMin > _contractQuantityFilterMax)
        {
            _contractQuantityFilterMax = _contractQuantityFilterMin;
        }

        if (GUILayout.Button("Reset Contract Filters", GUILayout.Height(26f)))
        {
            _contractDeliverabilityFilter = ContractDeliverabilityFilter.All;
            _contractSpecificationFilter = ContractSpecificationFilter.All;
            _contractRequestedCategoryFilterEnabled = false;
            _contractRequestedCategoryFilter = AircraftCategory.LightSportAndRecreational;
            _contractQuantityFilterMin = 5;
            _contractQuantityFilterMax = 500;
        }

        if (GUILayout.Button("Go To Airframe + Type Tab", GUILayout.Height(30f)))
        {
            _tab = 1;
        }

        if (_lastGeneratedContracts.Count > 0)
        {
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
        else
        {
            GUILayout.Label("No generated contract batch in this session yet.");
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Past Orders", GUILayout.Height(30f)))
        {
            _showPastOrdersWindow = true;
        }
    }

    private void DrawContractsReport()
    {
        DrawSection("Contract Board");
        if (_contractBoard is null || _productionFactory is null)
        {
            DrawError("Contract or factory system is not initialized.");
            return;
        }

        var catalog = BuildProductionCatalog();
        PruneContractDeliverySelections(_contractBoard.OpenContracts, catalog);
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
            IReadOnlyList<ContractDeliveryAllocation>? deliveryAllocationsToApply = null;
            double contractIncomeToBook = 0d;
            double contractProfitLossToBook = 0d;
            string deliveryPlanSummaryToBook = string.Empty;

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

                var deliveryCandidates = BuildContractDeliveryCandidates(
                    contract,
                    _contractBoard,
                    catalog,
                    warehouseSnapshot);

                if (deliveryCandidates.Count == 0)
                {
                    DrawError("Status: blocked");
                    var categoryCompatibleWarehouseUnits = warehouseSnapshot
                        .Where(entry => entry.Value > 0 && catalog.TryGetValue(entry.Key, out var item) && IsCatalogItemCategoryCompatibleWithContract(contract, item))
                        .Sum(entry => entry.Value);
                    if (categoryCompatibleWarehouseUnits <= 0 && contract.Requirements.RequiredCategory.HasValue)
                    {
                        var requiredCategoryName =
                            AircraftCategoryCatalog.GetProfile(contract.Requirements.RequiredCategory.Value).DisplayName;
                        GUILayout.Label($"- No warehouse aircraft matches required category '{requiredCategoryName}'.");
                    }
                    else
                    {
                        GUILayout.Label("- No usable aircraft in warehouse.");
                    }

                    if (warehouseSnapshot.Count > 0 && catalog.Count == 0)
                    {
                        GUILayout.Label("- Save at least one aircraft design in Airframe + Type tab.");
                    }

                    if (categoryCompatibleWarehouseUnits > 0)
                    {
                        GUILayout.Label("- Available aircraft in matching category do not meet one or more contract requirements.");
                    }
                }
                else
                {
                    if (_excludedWarehouseDesignByContractId.TryGetValue(contract.Id, out var excludedDesignIds))
                    {
                        var validDesignIds = new HashSet<string>(deliveryCandidates.Select(candidate => candidate.DesignId), StringComparer.Ordinal);
                        excludedDesignIds.IntersectWith(validDesignIds);
                        if (excludedDesignIds.Count == 0)
                        {
                            _excludedWarehouseDesignByContractId.Remove(contract.Id);
                            excludedDesignIds = null;
                        }
                    }

                    GUILayout.Label("Included delivery sources (uncheck to exclude for this contract):");
                    var includedCandidates = new List<ContractDeliveryCandidate>(deliveryCandidates.Count);
                    for (var candidateIndex = 0; candidateIndex < deliveryCandidates.Count; candidateIndex++)
                    {
                        var candidate = deliveryCandidates[candidateIndex];
                        var isIncluded = excludedDesignIds is null || !excludedDesignIds.Contains(candidate.DesignId);
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        var nextIncluded = GUILayout.Toggle(isIncluded, string.Empty, GUILayout.Width(22f));
                        GUILayout.Label(candidate.CatalogItem.DisplayName, GUILayout.Width(260f));
                        GUILayout.Label($"stock {FormatUiInteger(candidate.StockUnits)}", GUILayout.Width(110f));
                        GUILayout.Label($"unit cost {FormatCurrency(candidate.UnitFulfillmentCost)}");
                        GUILayout.EndHorizontal();

                        if (nextIncluded != isIncluded)
                        {
                            if (!nextIncluded)
                            {
                                excludedDesignIds ??= new HashSet<string>(StringComparer.Ordinal);
                                excludedDesignIds.Add(candidate.DesignId);
                                _excludedWarehouseDesignByContractId[contract.Id] = excludedDesignIds;
                            }
                            else if (excludedDesignIds is not null)
                            {
                                excludedDesignIds.Remove(candidate.DesignId);
                                if (excludedDesignIds.Count == 0)
                                {
                                    _excludedWarehouseDesignByContractId.Remove(contract.Id);
                                    excludedDesignIds = null;
                                }
                            }
                        }

                        if (nextIncluded)
                        {
                            includedCandidates.Add(candidate);
                        }
                    }

                    var includedStock = includedCandidates.Sum(candidate => candidate.StockUnits);
                    DrawMetric("Stock In Selected Sources", includedStock, "0");

                    var deliveryPlan = BuildCheapestContractDeliveryPlan(contract.QuantityUnits, includedCandidates);
                    if (deliveryPlan.Count == 0)
                    {
                        DrawError("Status: blocked");
                        if (includedCandidates.Count == 0)
                        {
                            GUILayout.Label("- All matching aircraft were excluded. Re-enable at least one source.");
                        }
                        else
                        {
                            GUILayout.Label(
                                $"- Selected sources provide {FormatUiInteger(includedStock)} units, " +
                                $"needs {FormatUiInteger(contract.QuantityUnits)}.");
                        }
                    }
                    else
                    {
                        var mainSourceAircraft = deliveryPlan[0].Candidate.CatalogItem.Aircraft;
                        var mainSourceCategory = deliveryPlan[0].Candidate.CatalogItem.Category;
                        DrawMetric("Source Reliability", mainSourceAircraft.Performance.OverallReliability, "0.000");
                        DrawMetric("Source Weight (kg)", mainSourceAircraft.Performance.TotalWeight.Kilograms, "0.0");
                        DrawMetric("Source Range (km)", mainSourceAircraft.Performance.RangeKilometers, "0.0");
                        DrawMetric("Source Payload (kg)", mainSourceAircraft.Performance.PayloadCapacity.Kilograms, "0.0");
                        DrawMetric("Source Cruise Fuel Burn (kg/h)", mainSourceAircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour, "0.0");
                        GUILayout.Label($"Main Source Category: {AircraftCategoryCatalog.GetProfile(mainSourceCategory).DisplayName}");

                        GUILayout.Label("Delivery plan (cheapest first):");
                        for (var allocationIndex = 0; allocationIndex < deliveryPlan.Count; allocationIndex++)
                        {
                            var allocation = deliveryPlan[allocationIndex];
                            GUILayout.Label(
                                $"- {FormatUiInteger(allocation.UnitsToDeliver)}x {allocation.Candidate.CatalogItem.DisplayName} " +
                                $"@ {FormatCurrency(allocation.Candidate.UnitFulfillmentCost)} / unit");
                        }

                        var contractUnitPrice = GetContractUnitPrice(contract);
                        var contractTotalPrice = GetContractTotalPrice(contract);
                        var fulfillmentCost = deliveryPlan.Sum(allocation => allocation.Candidate.UnitFulfillmentCost * allocation.UnitsToDeliver);
                        var expectedProfitLoss = contractTotalPrice - fulfillmentCost;
                        DrawMetric("Contract Unit Price", contractUnitPrice, "0.00");
                        DrawMetric("Contract Total Price", contractTotalPrice, "0.00");
                        DrawSignedCurrencyMetric("Expected Profit/Loss", expectedProfitLoss);
                        GUILayout.Label("Status: deliverable");
                        if (GUILayout.Button($"Deliver {FormatUiInteger(contract.QuantityUnits)} Units", GUILayout.Height(26f)))
                        {
                            contractToDeliver = contract;
                            deliveryAllocationsToApply = deliveryPlan;
                            contractIncomeToBook = contractTotalPrice;
                            contractProfitLossToBook = expectedProfitLoss;
                            deliveryPlanSummaryToBook = BuildContractDeliveryPlanSummary(deliveryPlan);
                        }
                    }
                }

                GUILayout.EndVertical();
            }

            if (contractToDeliver is not null
                && deliveryAllocationsToApply is not null
                && deliveryAllocationsToApply.Count > 0)
            {
                var withdrawnAllocations = new List<ContractDeliveryAllocation>(deliveryAllocationsToApply.Count);
                string? withdrawalFailureMessage = null;
                for (var allocationIndex = 0; allocationIndex < deliveryAllocationsToApply.Count; allocationIndex++)
                {
                    var allocation = deliveryAllocationsToApply[allocationIndex];
                    if (!_productionFactory.TryWithdrawFromWarehouse(
                            allocation.Candidate.DesignId,
                            allocation.UnitsToDeliver,
                            out var withdrawalMessage))
                    {
                        withdrawalFailureMessage = withdrawalMessage;
                        break;
                    }

                    withdrawnAllocations.Add(allocation);
                }

                if (!string.IsNullOrWhiteSpace(withdrawalFailureMessage))
                {
                    for (var rollbackIndex = 0; rollbackIndex < withdrawnAllocations.Count; rollbackIndex++)
                    {
                        var allocation = withdrawnAllocations[rollbackIndex];
                        _productionFactory.AddWarehouseUnits(allocation.Candidate.DesignId, allocation.UnitsToDeliver);
                    }

                    _contractActionMessage = $"Delivery failed. {withdrawalFailureMessage} Warehouse stock rollback applied.";
                }
                else
                {
                    var leadAircraft = deliveryAllocationsToApply[0].Candidate.CatalogItem.Aircraft;
                    var result = _contractBoard.TryDeliver(contractToDeliver.Id, leadAircraft);
                    if (!result.IsSuccess)
                    {
                        for (var rollbackIndex = 0; rollbackIndex < deliveryAllocationsToApply.Count; rollbackIndex++)
                        {
                            var allocation = deliveryAllocationsToApply[rollbackIndex];
                            _productionFactory.AddWarehouseUnits(allocation.Candidate.DesignId, allocation.UnitsToDeliver);
                        }

                        _contractActionMessage = $"{result.Message} Warehouse stock rollback applied.";
                    }
                    else
                    {
                        _selectedWarehouseDesignByContractId.Remove(contractToDeliver.Id);
                        _excludedWarehouseDesignByContractId.Remove(contractToDeliver.Id);
                        _contractActionMessage =
                            $"{result.Message} Delivered from: {deliveryPlanSummaryToBook}. Revenue booked: {FormatCurrency(contractIncomeToBook)}.";
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
                                inventoryByDesign,
                                $"Contract delivery income: {contractToDeliver.Id}");
                        }
                    }
                }
            }
        }

        var pastOrdersCount = _contractBoard.DeliveredContracts.Count + _contractBoard.CanceledContracts.Count;
        DrawSection("Past Orders");
        DrawMetric("Archived Orders", pastOrdersCount, "0");
        GUILayout.Label("Use the 'Past Orders' button in Contracts tab (left bottom) to review fulfilled and canceled orders.");
    }

    private void DrawPastOrdersWindow(Vector2 virtualScreenSize)
    {
        if (!_showPastOrdersWindow || _contractBoard is null)
        {
            return;
        }

        var minWidth = 780f;
        var minHeight = 460f;
        var maxX = System.Math.Max(virtualScreenSize.x - _pastOrdersWindowRect.width - 10f, 10f);
        var maxY = System.Math.Max(virtualScreenSize.y - _pastOrdersWindowRect.height - 10f, 10f);
        _pastOrdersWindowRect.x = Mathf.Clamp(_pastOrdersWindowRect.x, 10f, maxX);
        _pastOrdersWindowRect.y = Mathf.Clamp(_pastOrdersWindowRect.y, 10f, maxY);
        _pastOrdersWindowRect.width = Mathf.Clamp(_pastOrdersWindowRect.width, minWidth, virtualScreenSize.x - 20f);
        _pastOrdersWindowRect.height = Mathf.Clamp(_pastOrdersWindowRect.height, minHeight, virtualScreenSize.y - 20f);

        _pastOrdersWindowRect = GUI.Window(PastOrdersWindowId, _pastOrdersWindowRect, DrawPastOrdersWindowContents, "Past Orders");
    }

    private void DrawPastOrdersWindowContents(int windowId)
    {
        if (_contractBoard is null)
        {
            GUILayout.Label("Contract board is not initialized.");
            if (GUILayout.Button("Close", GUILayout.Width(120f), GUILayout.Height(26f)))
            {
                _showPastOrdersWindow = false;
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
            return;
        }

        DrawRect(
            new Rect(0f, 0f, _pastOrdersWindowRect.width, _pastOrdersWindowRect.height),
            new Color(0.12f, 0.12f, 0.12f, 1f));

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Fulfilled: {FormatUiInteger(_contractBoard.DeliveredContracts.Count)}", GUILayout.Width(170f));
        GUILayout.Label($"Canceled: {FormatUiInteger(_contractBoard.CanceledContracts.Count)}", GUILayout.Width(170f));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", GUILayout.Width(120f), GUILayout.Height(24f)))
        {
            _showPastOrdersWindow = false;
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        _pastOrdersScroll = GUILayout.BeginScrollView(_pastOrdersScroll);
        if (_contractBoard.DeliveredContracts.Count == 0 && _contractBoard.CanceledContracts.Count == 0)
        {
            GUILayout.Label("No past orders yet.");
        }
        else
        {
            if (_contractBoard.DeliveredContracts.Count > 0)
            {
                DrawSection("Fulfilled Contracts");
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

            if (_contractBoard.CanceledContracts.Count > 0)
            {
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
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
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
        if (_runtimeScreen != RuntimeScreen.Gameplay)
        {
            return;
        }

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
        lines.Add($"- Minimum Maneuverability: {FormatUiNumber(requirements.MinimumManeuverability, "0.0")}");
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
        GUILayout.Label("Accepting grants immediate upfront funding.");
        GUILayout.Label("Delivery contracts are fulfilled manually from warehouse stock.");

        var activeContract = _governmentContractBoard.ActiveContract;
        var activeDeliveryContracts = _governmentContractBoard.ActiveDeliveryContracts;
        var activeDelivery = activeDeliveryContracts.Count == 0
            ? null
            : activeDeliveryContracts[0];
        GUILayout.Label($"Active Contract: {(activeContract is null ? "None" : $"{activeContract.ContractId} ({activeContract.Status})")}");
        GUILayout.Label(
            $"Active Deliveries: {FormatUiInteger(activeDeliveryContracts.Count)}" +
            (activeDelivery is null ? string.Empty : $" | Latest: {activeDelivery.DeliveryId}"));
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
                        DrawMetric("Reliability", candidate.Metrics.Reliability, "0.000");
                        DrawMetric("Maneuverability", candidate.Metrics.Maneuverability, "0.0");
                        DrawMetric("Range (km)", candidate.Metrics.RangeKilometers, "0.0");
                        DrawMetric("Payload (kg)", candidate.Metrics.PayloadKilograms, "0.0");
                        DrawMetric("Runway Length (m)", candidate.Metrics.RequiredRunwayLengthMeters, "0.0");
                        DrawMetric("Development Price - Engine", candidate.DevelopmentPriceBreakdown.EnginePrice, "0.00");
                        DrawMetric("Development Price - Family", candidate.DevelopmentPriceBreakdown.FamilyPrice, "0.00");
                        DrawMetric("Development Price - Type", candidate.DevelopmentPriceBreakdown.TypePrice, "0.00");
                        DrawMetric("Eligible Development Price (Total)", candidate.DevelopmentPriceBreakdown.TotalPrice, "0.00");
                        DrawMetric(
                            "Upfront Funding (75%)",
                            candidate.DevelopmentPriceBreakdown.TotalPrice * GovernmentContractBalance.UpfrontFundingShare,
                            "0.00");

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
                                    inventoryByDesign,
                                    $"Government upfront funding: {activeContract.ContractId}");
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
        var activeDeliveries = _governmentContractBoard.ActiveDeliveryContracts;
        if (activeDeliveries.Count == 0)
        {
            GUILayout.Label("No active government delivery contract.");
        }
        else
        {
            for (var index = 0; index < activeDeliveries.Count; index++)
            {
                var activeDelivery = activeDeliveries[index];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{activeDelivery.DeliveryId} | Source: {ResolveAircraftDesignName(activeDelivery.AircraftDesignId)}");
                GUILayout.Label($"Status: {activeDelivery.Status}");
                GUILayout.Label($"Active Window: {FormatSimulationDate(activeDelivery.StartedOn)} -> {FormatSimulationDate(activeDelivery.EndsOn)}");
                DrawMetric("Required Units", activeDelivery.TotalUnitsRequired, "0");
                DrawMetric("Delivered Units", activeDelivery.DeliveredUnits, "0");
                DrawMetric("Remaining Units", activeDelivery.RemainingUnits, "0");
                DrawMetric("Unit Price", activeDelivery.UnitPrice, "0.00");
                DrawMetric("Completion Bonus", activeDelivery.TotalContractValue * GovernmentContractBalance.DeliveryCompletionBonusShare, "0.00");
                DrawMetric("Total Contract Value", activeDelivery.TotalContractValue, "0.00");

                var availableInWarehouse = _productionFactory.GetWarehouseUnits(activeDelivery.AircraftDesignId);
                DrawMetric("Warehouse Units (Selected Type)", availableInWarehouse, "0");

                if (!_governmentDeliveryQuantityInputById.TryGetValue(activeDelivery.DeliveryId, out var quantityInput)
                    || string.IsNullOrWhiteSpace(quantityInput))
                {
                    quantityInput = "1";
                }

                quantityInput = DrawText("Deliver Units", quantityInput);
                _governmentDeliveryQuantityInputById[activeDelivery.DeliveryId] = quantityInput;
                var parsedQuantity = TryParsePositiveInt(quantityInput, out var requestedUnits)
                    ? requestedUnits
                    : 0;

                GUILayout.BeginHorizontal();
                var canDeliverManually = availableInWarehouse > 0 && activeDelivery.RemainingUnits > 0;
                GUI.enabled = canDeliverManually && parsedQuantity > 0;
                if (GUILayout.Button("Deliver Aircraft", GUILayout.Height(24f)))
                {
                    ProcessGovernmentDelivery(activeDelivery.DeliveryId, parsedQuantity);
                }

                GUI.enabled = canDeliverManually;
                if (GUILayout.Button("Deliver All In Warehouse", GUILayout.Height(24f)))
                {
                    var allUnits = System.Math.Min(availableInWarehouse, activeDelivery.RemainingUnits);
                    ProcessGovernmentDelivery(activeDelivery.DeliveryId, allUnits);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
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

    private void ProcessGovernmentDelivery(string deliveryId, int requestedUnits)
    {
        if (_governmentContractBoard is null || _governmentInventoryGateway is null || _productionFactory is null)
        {
            _governmentDeliveryActionMessage = "Government delivery system is not initialized.";
            return;
        }

        if (requestedUnits <= 0)
        {
            _governmentDeliveryActionMessage = "Delivery quantity must be greater than zero.";
            return;
        }

        if (!_governmentContractBoard.TryGetActiveDeliveryContract(deliveryId, out var deliveryContractBefore)
            || deliveryContractBefore is null)
        {
            _governmentDeliveryActionMessage = $"Delivery contract '{deliveryId}' is no longer active.";
            _governmentDeliveryQuantityInputById.Remove(deliveryId);
            return;
        }

        var deliveryUpdate = _governmentContractBoard.DeliverFromInventory(
            _currentDate,
            deliveryId,
            _governmentInventoryGateway,
            requestedUnits);

        var deliveryIncome = 0d;
        if (deliveryUpdate.DeliveredUnitsThisMonth > 0)
        {
            deliveryIncome = deliveryUpdate.DeliveredUnitsThisMonth * deliveryContractBefore.UnitPrice;
        }

        var completionBonusIncome = deliveryUpdate.CompletionBonusGranted > 0d
            ? deliveryUpdate.CompletionBonusGranted
            : 0d;
        var bookedIncome = deliveryIncome + completionBonusIncome;

        if (_accountingSystem is not null && bookedIncome > 0d)
        {
            var fullCatalog = BuildProductionCatalog(includeOnlyResearched: false);
            var inventoryByDesign = _factoryAccountingCalculator.CalculateInventoryValueByDesign(
                _productionFactory.WarehouseUnitsByDesign,
                fullCatalog);

            if (deliveryIncome > 0d)
            {
                _accountingSystem.RegisterContractIncome(
                    _elapsedMonths,
                    _currentDate,
                    deliveryIncome,
                    inventoryByDesign,
                    $"Government delivery income: {deliveryContractBefore.DeliveryId}");
            }

            if (completionBonusIncome > 0d)
            {
                _accountingSystem.RegisterContractIncome(
                    _elapsedMonths,
                    _currentDate,
                    completionBonusIncome,
                    inventoryByDesign,
                    $"Government completion bonus: {deliveryContractBefore.DeliveryId}");
            }

            _governmentDeliveryActionMessage = $"{deliveryUpdate.Message} Revenue booked: {FormatCurrency(bookedIncome)}.";
        }
        else
        {
            _governmentDeliveryActionMessage = deliveryUpdate.Message;
        }

        if (!_governmentContractBoard.TryGetActiveDeliveryContract(deliveryId, out _))
        {
            _governmentDeliveryQuantityInputById.Remove(deliveryId);
        }
    }

    private static bool TryParsePositiveInt(string rawValue, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (!int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed <= 0)
        {
            return false;
        }

        value = parsed;
        return true;
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
                var submission = BuildGovernmentSubmissionCandidate(savedDesign, lifecycleStage);
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

    private GovernmentContractSubmissionCandidate BuildGovernmentSubmissionCandidate(
        SavedAircraftDesign savedDesign,
        WrightTycoon.Core.GovernmentContracts.AircraftLifecycleStage lifecycleStage)
    {
        if (savedDesign is null)
        {
            throw new ArgumentNullException(nameof(savedDesign));
        }

        var breakdown = BuildGovernmentDevelopmentPriceBreakdown(savedDesign);
        return GovernmentContractSubmissionCandidateFactory.FromAircraft(
            savedDesign.CatalogItem.DesignId,
            savedDesign.CatalogItem.DisplayName,
            lifecycleStage,
            savedDesign.CatalogItem.Aircraft,
            breakdown);
    }

    private GovernmentContractDevelopmentPriceBreakdown BuildGovernmentDevelopmentPriceBreakdown(SavedAircraftDesign savedDesign)
    {
        if (savedDesign is null)
        {
            return GovernmentContractDevelopmentPriceBreakdown.FromTotal(0d);
        }

        var enginePrice = ResolveGovernmentResearchPrice(ResearchSubjectType.Engine, savedDesign.EngineResearchId);
        var familyPrice = ResolveGovernmentResearchPrice(ResearchSubjectType.AircraftFamily, savedDesign.FamilyResearchId);
        var typePrice = ResolveGovernmentResearchPrice(ResearchSubjectType.AircraftType, savedDesign.TypeResearchId);
        var breakdown = new GovernmentContractDevelopmentPriceBreakdown(enginePrice, familyPrice, typePrice);
        if (breakdown.TotalPrice <= 0d)
        {
            breakdown = GovernmentContractDevelopmentPriceBreakdown.FromTotal(
                System.Math.Max(savedDesign.CatalogItem.Aircraft.Development.DevelopmentCost, 0d));
        }

        return breakdown;
    }

    private double ResolveGovernmentResearchPrice(ResearchSubjectType subjectType, string subjectId)
    {
        if (_researchBoard is null || string.IsNullOrWhiteSpace(subjectId))
        {
            return 0d;
        }

        return _researchBoard.TryGetResearchCost(subjectType, subjectId, out var price)
            ? System.Math.Max(0d, price)
            : 0d;
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
        DrawMetric("Minimum Maneuverability", requirements.MinimumManeuverability, "0.0");
        DrawMetric("Minimum Crew", requirements.MinimumCrewSize, "0");
        DrawMetric("Maximum Runway Length (m)", requirements.MaximumRequiredRunwayLengthMeters, "0.0");
    }

    private void DrawDemandTab()
    {
        DrawSection("Demand");
        var currentYear = _currentDate.Year;
        var forecastYear = System.Math.Min(CoreBalance.HistoricalCampaignTechnology.EndYear, currentYear + 3);
        GUILayout.Label("Year-scaled demand uses min/max type requirements (reliability/range/speed/payload/maneuverability).");
        DrawMetric("Current Year", currentYear, "0");
        DrawMetric("Forecast Year (+3)", forecastYear, "0");
        DrawMetric("Demand Multiplier (Current)", TypeRequirementBalance.ResolveMinimumRequirementMultiplier(currentYear), "0.000");
        DrawMetric("Demand Multiplier (+3)", TypeRequirementBalance.ResolveMinimumRequirementMultiplier(forecastYear), "0.000");
        GUILayout.Space(6f);
        GUILayout.Label("Detailed type list is shown on the right panel.");
    }

    private void DrawDemandReport()
    {
        DrawSection("Demand");
        var currentYear = _currentDate.Year;
        var forecastYear = System.Math.Min(CoreBalance.HistoricalCampaignTechnology.EndYear, currentYear + 3);
        GUILayout.Label($"Type requirements preview: {FormatUiInteger(currentYear)} vs {FormatUiInteger(forecastYear)}.");

        var currentMinimumReliability = TypeRequirementBalance.ScaleMinimumReliability(ContractBalance.MinReliability, currentYear);
        var forecastMinimumReliability = TypeRequirementBalance.ScaleMinimumReliability(ContractBalance.MinReliability, forecastYear);
        var currentMaximumReliability = TypeRequirementBalance.ScaleMaximumReliability(ContractBalance.MaxReliability, currentYear);
        var forecastMaximumReliability = TypeRequirementBalance.ScaleMaximumReliability(ContractBalance.MaxReliability, forecastYear);
        var baseProfiles = AircraftCategoryCatalog.GetAllProfiles();
        for (var index = 0; index < baseProfiles.Count; index++)
        {
            var baseProfile = baseProfiles[index];
            var currentProfile = TypeRequirementBalance.CreateYearAdjustedProfile(baseProfile, currentYear);
            var forecastProfile = TypeRequirementBalance.CreateYearAdjustedProfile(baseProfile, forecastYear);

            DrawSection(currentProfile.DisplayName);
            DrawDemandRequirementRow("Minimum Reliability", currentMinimumReliability, forecastMinimumReliability, "0.000");
            DrawDemandRequirementRow("Maximum Reliability", currentMaximumReliability, forecastMaximumReliability, "0.000");
            DrawDemandRequirementRow("Minimum Range (km)", currentProfile.MinRequiredRangeKilometers, forecastProfile.MinRequiredRangeKilometers, "0.0");
            DrawDemandRequirementRow("Maximum Range (km)", currentProfile.MaxRequiredRangeKilometers, forecastProfile.MaxRequiredRangeKilometers, "0.0");
            DrawDemandRequirementRow("Minimum Speed (km/h)", currentProfile.MinRequiredSpeedKilometersPerHour, forecastProfile.MinRequiredSpeedKilometersPerHour, "0.0");
            DrawDemandRequirementRow("Maximum Speed (km/h)", currentProfile.MaxRequiredSpeedKilometersPerHour, forecastProfile.MaxRequiredSpeedKilometersPerHour, "0.0");
            DrawDemandRequirementRow("Minimum Payload (kg)", currentProfile.MinRequiredPayloadKilograms, forecastProfile.MinRequiredPayloadKilograms, "0.0");
            DrawDemandRequirementRow("Maximum Payload (kg)", currentProfile.MaxRequiredPayloadKilograms, forecastProfile.MaxRequiredPayloadKilograms, "0.0");
            DrawDemandRequirementRow("Minimum Maneuverability", currentProfile.MinRequiredManeuverability, forecastProfile.MinRequiredManeuverability, "0.0");
            DrawDemandRequirementRow("Maximum Maneuverability", currentProfile.MaxRequiredManeuverability, forecastProfile.MaxRequiredManeuverability, "0.0");
            DrawDemandSingleValueRow("Runway Lengths (m)", FormatCategoryRunwayLengths(currentProfile.Category));
        }
    }

    private static void DrawDemandRequirementRow(string label, double currentValue, double forecastValue, string format)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(220f));
        GUILayout.Label($"Now: {FormatUiNumber(currentValue, format)}", GUILayout.Width(210f));
        GUILayout.Label($"+3y: {FormatUiNumber(forecastValue, format)}");
        GUILayout.EndHorizontal();
    }

    private static void DrawDemandSingleValueRow(string label, string value)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(220f));
        GUILayout.Label(value);
        GUILayout.EndHorizontal();
    }

    private static string FormatCategoryRunwayLengths(AircraftCategory category)
    {
        var runwayLengths = ContractBalance.ResolveAllowedRunwayLengthMeters(category);
        if (runwayLengths.Length == 0)
        {
            return "N/A";
        }

        var parts = new string[runwayLengths.Length];
        for (var index = 0; index < runwayLengths.Length; index++)
        {
            parts[index] = $"{FormatUiNumber(runwayLengths[index], "0")} m";
        }

        return string.Join(" / ", parts);
    }

    private void DrawResearchTab()
    {
        DrawSection("Engineering (M4)");
        if (_researchBoard is null)
        {
            DrawError("Engineering board is not initialized.");
            return;
        }

        var proposed = GetVisibleResearchProjectsByStatus(ResearchStatus.Proposed);
        var ongoing = GetVisibleResearchProjectsByStatus(ResearchStatus.Ongoing);
        var completed = GetVisibleResearchProjectsByStatus(ResearchStatus.Completed);

        GUILayout.Label("Approve proposed engines/families/types and tune engineering modifiers per proposal. Ongoing projects advance every month.");
        DrawMetric("Proposed", proposed.Count, "0");
        DrawMetric("Ongoing", ongoing.Count, "0");
        DrawMetric("Completed", completed.Count, "0");

        _researchSubTab = Mathf.Clamp(_researchSubTab, 0, _researchSubTabs.Length - 1);
        _researchSubTab = GUILayout.Toolbar(_researchSubTab, _researchSubTabs, GUILayout.Height(30f));

        if (!string.IsNullOrWhiteSpace(_researchActionMessage))
        {
            GUILayout.Label(_researchActionMessage);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Engineering details and controls are shown on the right panel only.");
    }

    private void DrawResearchReport()
    {
        DrawSection("Engineering Board");
        if (_researchBoard is null)
        {
            DrawError("Engineering board is not initialized.");
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

        DrawSection("Current Engineering");
        var ongoing = GetVisibleResearchProjectsByStatus(ResearchStatus.Ongoing);
        if (ongoing.Count == 0)
        {
            GUILayout.Label("No ongoing engineering.");
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
                    DrawMetric("Engineering Cost", project.ResearchCost, "0.00");
                    DrawMetric("Complexity (%)", project.ComplexityNormalized * 100d, "0.0");
                    DrawMetric("Remaining Months", project.RemainingMonths, "0");
                    GUILayout.Label($"ID: {project.SubjectId}");
                }

                GUILayout.EndVertical();
            }
        }

        DrawSection("Completed Engineering");
        var completed = GetVisibleResearchProjectsByStatus(ResearchStatus.Completed);
        if (completed.Count == 0)
        {
            GUILayout.Label("No completed engineering yet.");
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
            GUILayout.Label($"Engineering Subject: {GetResearchSubjectLabel(project.SubjectType)}");
            GUILayout.Label($"{GetResearchNameFieldLabel(project.SubjectType)}: {displayName}");
            DrawMetric("Engineering Cost", project.ResearchCost, "0.00");
            DrawMetric("Engineering Time (months)", project.ResearchTimeMonths, "0");
            if (IsFirstYearResearchApprovalWindow())
            {
                GUILayout.Label(BuildResearchTimeBonusFormula(project.ResearchTimeMonths));
            }
            DrawMetric("Complexity (%)", project.ComplexityNormalized * 100d, "0.0");
            DrawResearchModifierEditor(project);
            if (isDetailed)
            {
                GUILayout.Label($"Engineering ID: {project.SubjectId}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Approve {GetResearchSubjectLabel(project.SubjectType)}", GUILayout.Height(24f)))
            {
                TryApproveResearchProject(project);
            }

            if (GUILayout.Button($"Cancel {GetResearchSubjectLabel(project.SubjectType)}", GUILayout.Height(24f)))
            {
                TryCancelResearchProject(project);
            }

            GUILayout.EndHorizontal();

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
            ApplyApprovedYearForApprovedProject(project, bookedDate);
            _researchActionMessage = $"{message} Expense booked in {FormatSimulationDate(bookedDate)}.";
            return;
        }

        _researchActionMessage = message;
    }

    private void TryCancelResearchProject(ResearchProject project)
    {
        if (_researchBoard is null)
        {
            return;
        }

        if (_researchBoard.TryCancelProposal(
            project.SubjectType,
            project.SubjectId,
            out _,
            out var message))
        {
            _researchActionMessage = message;
            return;
        }

        _researchActionMessage = message;
    }

    private bool IsFirstYearResearchApprovalWindow()
    {
        var bookingMonthIndex = _elapsedMonths + 1;
        return bookingMonthIndex >= 1
            && bookingMonthIndex <= ResearchBalance.FirstYearApprovalSpeedBonusWindowMonths;
    }

    private string BuildResearchTimeBonusFormula(int baseResearchMonths)
    {
        var normalizedBaseMonths = System.Math.Max(baseResearchMonths, 1);
        var bonusMonths = System.Math.Max(ResearchBalance.FirstYearApprovalSpeedBonusMonths, 0);
        var adjustedMonths = System.Math.Max(1, normalizedBaseMonths - bonusMonths);
        return $"Engineering time = {FormatUiInteger(normalizedBaseMonths)} - {FormatUiInteger(bonusMonths)} = {FormatUiInteger(adjustedMonths)}";
    }

    private void ApplyApprovedYearForApprovedProject(ResearchProject project, SimulationDate approvedDate)
    {
        if (project is null)
        {
            return;
        }

        if (project.SubjectType == ResearchSubjectType.Engine)
        {
            for (var index = 0; index < _savedEngines.Count; index++)
            {
                var savedEngine = _savedEngines[index];
                if (savedEngine.IsVersionDevelopment
                    || !string.Equals(savedEngine.ResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                savedEngine.SetApprovedYear(approvedDate.Year);
                savedEngine.SetFrozenDate(approvedDate);
            }
        }
        else if (project.SubjectType == ResearchSubjectType.AircraftType)
        {
            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (savedDesign.IsTypeVersionDevelopment
                    || !string.Equals(savedDesign.TypeResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                savedDesign.SetApprovedYear(approvedDate.Year);
                savedDesign.SetFrozenDate(approvedDate);
                if (!TryRebuildSavedAircraftCatalogItemAtDate(index, approvedDate, out var rebuildError))
                {
                    _researchActionMessage = $"Failed to freeze type '{savedDesign.Name}': {rebuildError}";
                }
            }
        }

        PropagateApprovedYearsToDerivedVariants();
    }

    private void BackfillApprovedYearsFromResearchBoard()
    {
        for (var index = 0; index < _savedEngines.Count; index++)
        {
            var savedEngine = _savedEngines[index];
            if (savedEngine.IsVersionDevelopment)
            {
                continue;
            }

            if (TryResolveApprovedDateFromResearchBoard(ResearchSubjectType.Engine, savedEngine.ResearchId, out var approvedDate))
            {
                if (!savedEngine.ApprovedYear.HasValue)
                {
                    savedEngine.SetApprovedYear(approvedDate.Year);
                }

                if (!savedEngine.HasFrozenDate)
                {
                    savedEngine.SetFrozenDate(approvedDate);
                }
            }
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var savedDesign = _savedAircraftDesigns[index];
            if (savedDesign.IsTypeVersionDevelopment)
            {
                continue;
            }

            if (TryResolveApprovedDateFromResearchBoard(ResearchSubjectType.AircraftType, savedDesign.TypeResearchId, out var approvedDate))
            {
                if (!savedDesign.ApprovedYear.HasValue)
                {
                    savedDesign.SetApprovedYear(approvedDate.Year);
                }

                if (!savedDesign.HasFrozenDate)
                {
                    savedDesign.SetFrozenDate(approvedDate);
                }

                if (savedDesign.TryGetFrozenDate(out var frozenDate))
                {
                    TryRebuildSavedAircraftCatalogItemAtDate(index, frozenDate, out _);
                }
            }
        }

        PropagateApprovedYearsToDerivedVariants();
    }

    private bool TryResolveApprovedYearFromResearchBoard(ResearchSubjectType subjectType, string subjectId, out int approvedYear)
    {
        approvedYear = 0;
        if (!TryResolveApprovedDateFromResearchBoard(subjectType, subjectId, out var approvedDate))
        {
            return false;
        }

        approvedYear = approvedDate.Year;
        return approvedYear >= 1;
    }

    private bool TryResolveApprovedDateFromResearchBoard(ResearchSubjectType subjectType, string subjectId, out SimulationDate approvedDate)
    {
        approvedDate = default;
        if (_researchBoard is null || string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        var projects = _researchBoard.GetAllProjects();
        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            if (project.SubjectType != subjectType || !string.Equals(project.SubjectId, subjectId, StringComparison.Ordinal))
            {
                continue;
            }

            if (project.Status == ResearchStatus.Proposed || !project.StartedMonthIndex.HasValue)
            {
                return false;
            }

            var campaignStart = ResolveCampaignStartDate();
            approvedDate = campaignStart.AddMonths(project.StartedMonthIndex.Value);
            return approvedDate.Year >= 1;
        }

        return false;
    }

    private bool TryResolveResearchTimeMonthsFromResearchBoard(
        ResearchSubjectType subjectType,
        string subjectId,
        out int researchTimeMonths)
    {
        researchTimeMonths = 0;
        if (_researchBoard is null || string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        var projects = _researchBoard.GetAllProjects();
        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            if (project.SubjectType != subjectType || !string.Equals(project.SubjectId, subjectId, StringComparison.Ordinal))
            {
                continue;
            }

            researchTimeMonths = System.Math.Max(project.ResearchTimeMonths, 1);
            return true;
        }

        return false;
    }

    private void PropagateApprovedYearsToDerivedVariants()
    {
        var maxIterations = _savedEngines.Count + _savedAircraftDesigns.Count + 1;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var changed = false;

            for (var index = 0; index < _savedEngines.Count; index++)
            {
                var savedEngine = _savedEngines[index];
                if (!savedEngine.IsVersionDevelopment)
                {
                    continue;
                }

                var sourceIndex = FindSavedEngineIndexByResearchId(savedEngine.SourceResearchId);
                if (sourceIndex < 0 || sourceIndex >= _savedEngines.Count)
                {
                    continue;
                }

                var sourceEngine = _savedEngines[sourceIndex];
                var inheritedYear = sourceEngine.ApprovedYear;
                var inheritedFrozenYear = sourceEngine.FrozenYear;
                var inheritedFrozenMonth = sourceEngine.FrozenMonth;
                if (savedEngine.ApprovedYear == inheritedYear
                    && savedEngine.FrozenYear == inheritedFrozenYear
                    && savedEngine.FrozenMonth == inheritedFrozenMonth)
                {
                    continue;
                }

                savedEngine.SetApprovedYear(inheritedYear);
                savedEngine.SetFrozenDate(inheritedFrozenYear, inheritedFrozenMonth);
                changed = true;
            }

            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (!savedDesign.IsTypeVersionDevelopment)
                {
                    continue;
                }

                var sourceIndex = FindSavedAircraftDesignIndexByTypeResearchId(savedDesign.SourceTypeResearchId);
                if (sourceIndex < 0 || sourceIndex >= _savedAircraftDesigns.Count)
                {
                    continue;
                }

                var sourceDesign = _savedAircraftDesigns[sourceIndex];
                var inheritedYear = sourceDesign.ApprovedYear;
                var inheritedFrozenYear = sourceDesign.FrozenYear;
                var inheritedFrozenMonth = sourceDesign.FrozenMonth;
                if (savedDesign.ApprovedYear == inheritedYear
                    && savedDesign.FrozenYear == inheritedFrozenYear
                    && savedDesign.FrozenMonth == inheritedFrozenMonth)
                {
                    continue;
                }

                savedDesign.SetApprovedYear(inheritedYear);
                savedDesign.SetFrozenDate(inheritedFrozenYear, inheritedFrozenMonth);
                if (savedDesign.TryGetFrozenDate(out var inheritedFrozenDate))
                {
                    TryRebuildSavedAircraftCatalogItemAtDate(index, inheritedFrozenDate, out _);
                }

                changed = true;
            }

            if (!changed)
            {
                break;
            }
        }
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

        DrawSection($"{GetResearchSubjectLabel(project.SubjectType)} Engineering Modifiers");
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
        baseQuote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.Engine, researchId, baseQuote);
        modifiedQuote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.Engine, researchId, modifiedQuote);
        baseQuote = ApplyEarlyResearchDiscountToResearchQuote(baseQuote);
        modifiedQuote = ApplyEarlyResearchDiscountToResearchQuote(modifiedQuote);
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
            "Engineering Cost",
            CalculatePercentageDelta(baseQuote.ResearchCost, modifiedQuote.ResearchCost),
            modifiedQuote.ResearchCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Engineering Time (months)",
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
        baseQuote = ApplyEarlyResearchDiscountToResearchQuote(baseQuote);
        modifiedQuote = ApplyEarlyResearchDiscountToResearchQuote(modifiedQuote);
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
            "Engineering Cost",
            CalculatePercentageDelta(baseQuote.ResearchCost, modifiedQuote.ResearchCost),
            modifiedQuote.ResearchCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Engineering Time (months)",
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
        baseQuote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.AircraftType, typeResearchId, baseQuote);
        modifiedQuote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.AircraftType, typeResearchId, modifiedQuote);
        baseQuote = ApplyEarlyResearchDiscountToResearchQuote(baseQuote);
        modifiedQuote = ApplyEarlyResearchDiscountToResearchQuote(modifiedQuote);
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
            "Engineering Cost",
            CalculatePercentageDelta(baseQuote.ResearchCost, modifiedQuote.ResearchCost),
            modifiedQuote.ResearchCost,
            "0.00");
        DrawModifierPercentagePreview(
            "Engineering Time (months)",
            CalculatePercentageDelta(baseQuote.ResearchTimeMonths, modifiedQuote.ResearchTimeMonths),
            modifiedQuote.ResearchTimeMonths,
            "0");
    }

    private bool TryBuildSavedEngineSnapshot(string researchId, out Engine engine)
    {
        engine = null!;
        for (var index = 0; index < _savedEngines.Count; index++)
        {
            var savedEngine = _savedEngines[index];
            if (!string.Equals(savedEngine.ResearchId, researchId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                engine = BuildSavedEngineAtReferenceDate(savedEngine);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private Engine BuildSavedEngineAtReferenceDate(SavedEngineDesign savedEngine)
    {
        if (savedEngine is null)
        {
            throw new ArgumentNullException(nameof(savedEngine));
        }

        if (savedEngine.TryGetFrozenDate(out var frozenDate))
        {
            return BuildEngineForDate(savedEngine.Definition, savedEngine.Parameters.Clone(), frozenDate);
        }

        if (_engineBuilder is null)
        {
            throw new InvalidOperationException("Engine builder is not initialized.");
        }

        return _engineBuilder.Build(savedEngine.Definition, savedEngine.Parameters.Clone());
    }

    private Engine BuildEngineForDate(EngineDefinition definition, EngineParameters parameters, SimulationDate date)
    {
        var technologyModifierSource = new HistoricalCampaignRuntimeTechnologyModifierSource(
            _historicalCampaignTechnologyProgression,
            () => date);
        var datedEngineBuilder = new EngineBuilder(
            new EngineCalculator(),
            _atmosphereModel,
            technologyModifierSource);
        return datedEngineBuilder.Build(definition, parameters);
    }

    private Aircraft BuildAircraftForDate(
        AircraftFamilyDefinition familyDefinition,
        AircraftTypeDefinition typeDefinition,
        SimulationDate date)
    {
        if (_engineRepo is null || _airframeRepo is null)
        {
            throw new InvalidOperationException("Repositories are not initialized.");
        }

        var technologyModifierSource = new HistoricalCampaignRuntimeTechnologyModifierSource(
            _historicalCampaignTechnologyProgression,
            () => date);
        var datedEngineBuilder = new EngineBuilder(
            new EngineCalculator(),
            _atmosphereModel,
            technologyModifierSource);
        var datedAirframeBuilder = new AirframeBuilder(
            new AirframeCalculator(_aerodynamicsCalculator, _atmosphereModel),
            technologyModifierSource);
        var datedAircraftFactory = new AircraftFactory(
            datedEngineBuilder,
            datedAirframeBuilder,
            new AircraftPerformanceCalculator(_atmosphereModel, _aerodynamicsCalculator),
            new DevelopmentCostCalculator(),
            _engineRepo,
            _airframeRepo);
        return datedAircraftFactory.CreateAircraft(familyDefinition, typeDefinition);
    }

    private bool TryRebuildSavedAircraftCatalogItemAtDate(int savedDesignIndex, SimulationDate date, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (savedDesignIndex < 0 || savedDesignIndex >= _savedAircraftDesigns.Count)
        {
            errorMessage = "Saved aircraft design index is out of range.";
            return false;
        }

        var savedDesign = _savedAircraftDesigns[savedDesignIndex];
        try
        {
            var rebuiltAircraft = BuildAircraftForDate(
                savedDesign.CatalogItem.Aircraft.FamilyDefinition,
                savedDesign.CatalogItem.Aircraft.TypeDefinition,
                date);
            var rebuiltCatalogItem = new FactoryAircraftTypeCatalogItem(
                savedDesign.CatalogItem.DesignId,
                savedDesign.CatalogItem.DisplayName,
                rebuiltAircraft,
                savedDesign.CatalogItem.RequiredFactoryToolLevel,
                1);

            _savedAircraftDesigns[savedDesignIndex] = new SavedAircraftDesign(
                savedDesign.Name,
                rebuiltCatalogItem,
                savedDesign.EngineResearchId,
                savedDesign.FamilyResearchId,
                savedDesign.TypeResearchId,
                savedDesign.DevelopmentStartedYear,
                savedDesign.VariantDevelopmentStartedYear,
                savedDesign.TypeVersionDevelopmentBaselineYear,
                savedDesign.SourceTypeResearchId,
                savedDesign.ApprovedYear,
                savedDesign.FrozenYear,
                savedDesign.FrozenMonth);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
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
            RequiredLineToolLevel = 1,
        };

        try
        {
            if (savedDesign.TryGetFrozenDate(out var frozenDate))
            {
                aircraft = BuildAircraftForDate(savedDesign.CatalogItem.Aircraft.FamilyDefinition, baselineType, frozenDate);
            }
            else
            {
                aircraft = _aircraftFactory.CreateAircraft(savedDesign.CatalogItem.Aircraft.FamilyDefinition, baselineType);
            }

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
            for (var index = 0; index < _savedEngines.Count; index++)
            {
                var savedEngine = _savedEngines[index];
                if (!string.Equals(savedEngine.ResearchId, project.SubjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    var engineSnapshot = BuildSavedEngineAtReferenceDate(savedEngine);
                    displayName = savedEngine.Name;
                    quote = _researchCalculator.CalculateEngineQuote(engineSnapshot, modifierPoints);
                    quote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.Engine, project.SubjectId, quote);
                    quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
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
                quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
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
                quote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.AircraftType, project.SubjectId, quote);
                quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
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

            if (savedDesign.HasFrozenDate || savedDesign.ApprovedYear.HasValue)
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
                RequiredLineToolLevel = 1,
            };

            try
            {
                var rebuiltAircraft = _aircraftFactory.CreateAircraft(aircraft.FamilyDefinition, updatedType);
                var updatedCatalogItem = new FactoryAircraftTypeCatalogItem(
                    savedDesign.CatalogItem.DesignId,
                    savedDesign.CatalogItem.DisplayName,
                    rebuiltAircraft,
                    savedDesign.CatalogItem.RequiredFactoryToolLevel,
                    1);

                _savedAircraftDesigns[index] = new SavedAircraftDesign(
                    savedDesign.Name,
                    updatedCatalogItem,
                    savedDesign.EngineResearchId,
                    savedDesign.FamilyResearchId,
                    savedDesign.TypeResearchId,
                    savedDesign.DevelopmentStartedYear,
                    savedDesign.VariantDevelopmentStartedYear,
                    savedDesign.TypeVersionDevelopmentBaselineYear,
                    savedDesign.SourceTypeResearchId,
                    savedDesign.ApprovedYear,
                    savedDesign.FrozenYear,
                    savedDesign.FrozenMonth);
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
        DrawMetric("Recorded Transactions", _accountingSystem.GetTransactions().Count, "0");

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
        var transactions = _accountingSystem.GetTransactions();
        DrawMetric("Cash", _accountingSystem.Cash, "0.00");

        _financeSubTab = Mathf.Clamp(_financeSubTab, 0, _financeSubTabs.Length - 1);
        if (_financeSubTab == 0)
        {
            if (history.Count == 0)
            {
                GUILayout.Label("No financial history yet. Press Next Month to process production costs.");
                return;
            }

            DrawSection("Income / Expense Timeline");
            DrawIncomeExpenseTimelineChart(history);
            return;
        }

        if (_financeSubTab == 2)
        {
            DrawSection("Transactions");
            DrawFinanceTransactionsReport(transactions);
            return;
        }

        if (history.Count == 0)
        {
            GUILayout.Label("No financial history yet. Press Next Month to process production costs.");
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

    private void DrawFinanceTransactionsReport(IReadOnlyList<AccountingTransactionEntry> transactions)
    {
        if (transactions.Count == 0)
        {
            GUILayout.Label("No transactions recorded yet.");
            return;
        }

        for (var index = transactions.Count - 1; index >= 0; index--)
        {
            var transaction = transactions[index];
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(FormatSimulationDate(transaction.Date), GUILayout.Width(145f));
            GUILayout.Label(transaction.Description, GUILayout.Width(360f));
            DrawSignedCurrencyInline($"Amount: {FormatCurrency(transaction.Amount)}", transaction.Amount, 210f);
            GUILayout.Label($"Cash: {FormatCurrency(transaction.CashAfterTransaction)}");
            GUILayout.EndHorizontal();
        }
    }

    private void DrawMyMachinesTab()
    {
        DrawSection("My machines");
        GUILayout.Label("Manage saved engines and aircraft families/types from one place.");

        _myMachinesSubTab = Mathf.Clamp(_myMachinesSubTab, 0, _myMachinesSubTabs.Length - 1);
        _myMachinesSubTab = GUILayout.Toolbar(_myMachinesSubTab, _myMachinesSubTabs, GUILayout.Height(30f));

        if (_myMachinesSubTab == 0)
        {
            DrawMyMachinesEnginesSubTab();
            return;
        }

        DrawMyMachinesFamiliesAndTypesSubTab();
    }

    private void DrawMyMachinesReport()
    {
        DrawSection("My machines overview");
        DrawMetric("Saved Engines", _savedEngines.Count, "0");
        DrawMetric("Saved Families", _savedFamilies.Count, "0");
        DrawMetric("Saved Types", _savedAircraftDesigns.Count, "0");

        DrawSection("Development Mode");
        if (_engineVersionDevelopment is not null)
        {
            GUILayout.Label(
                $"Engine version: {_engineVersionDevelopment.SourceDisplayName} | " +
                $"development start {FormatUiInteger(_engineVersionDevelopment.SourceDevelopmentStartedYear)} | " +
                $"variant start {FormatUiInteger(_engineVersionDevelopment.VariantDevelopmentStartedYear)}");
            GUILayout.Label("Open Engine Designer to continue.");
        }
        else if (_typeVersionDevelopment is not null)
        {
            GUILayout.Label(
                $"Type version: {_typeVersionDevelopment.SourceDisplayName} | " +
                $"family {_typeVersionDevelopment.SourceFamilyDisplayName} | " +
                $"development start {FormatUiInteger(_typeVersionDevelopment.SourceDevelopmentStartedYear)} | " +
                $"variant start {FormatUiInteger(_typeVersionDevelopment.VariantDevelopmentStartedYear)}");
            GUILayout.Label("Open Airframe + Type Designer to continue.");
        }
        else
        {
            GUILayout.Label("No active version-development session.");
        }

        if (_myMachinesSubTab == 0)
        {
            DrawMyMachinesSelectedEngineReport();
            return;
        }

        DrawMyMachinesSelectedTypeReport();
    }

    private void DrawMyMachinesEnginesSubTab()
    {
        DrawSection("All Engines");
        if (_savedEngines.Count == 0)
        {
            GUILayout.Label("No engines saved yet.");
            return;
        }

        for (var index = 0; index < _savedEngines.Count; index++)
        {
            var savedEngine = _savedEngines[index];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(BuildEngineDisplayName(savedEngine.Name, savedEngine.Definition.EngineType, savedEngine.Parameters));
            GUILayout.Label($"Engineering: {ResolveResearchStatusLabel(ResearchSubjectType.Engine, savedEngine.ResearchId)}");
            GUILayout.Label($"Approved Year: {FormatApprovedYear(savedEngine.ApprovedYear)}");
            try
            {
                var built = BuildSavedEngineAtReferenceDate(savedEngine);
                var outputAtSeaLevel = ResolveEngineOutputAtSeaLevel(built);
                var outputLabel = built.Performance.OutputKind == EngineOutputKind.PowerKilowatts
                    ? "Engine Power @0m (kW)"
                    : "Engine Thrust @0m (N)";
                GUILayout.Label($"{outputLabel}: {FormatUiNumber(outputAtSeaLevel, "0.0")}");
            }
            catch
            {
                GUILayout.Label("Engine output @0m: N/A");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select", GUILayout.Width(90f), GUILayout.Height(24f)))
            {
                _selectedSavedEngineIndex = index;
            }

            if (GUILayout.Button("Open Engine Designer", GUILayout.Height(24f)))
            {
                _engineVersionDevelopment = null;
                _typeVersionDevelopment = null;
                LoadSavedEngine(index);
                _tab = 0;
            }

            if (GUILayout.Button("New Version", GUILayout.Height(24f)))
            {
                BeginEngineVersionDevelopment(index);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }

    private void DrawMyMachinesFamiliesAndTypesSubTab()
    {
        DrawSection("Families + Types");
        if (_savedFamilies.Count == 0 && _savedAircraftDesigns.Count == 0)
        {
            GUILayout.Label("No families or types saved yet.");
            return;
        }

        var familyIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < _savedFamilies.Count; index++)
        {
            familyIds.Add(_savedFamilies[index].FamilyId);
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            familyIds.Add(_savedAircraftDesigns[index].FamilyResearchId);
        }

        var orderedFamilyIds = familyIds
            .OrderBy(ResolveFamilyDisplayNameForMyMachines, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var familyIndex = 0; familyIndex < orderedFamilyIds.Length; familyIndex++)
        {
            var familyId = orderedFamilyIds[familyIndex];
            var familyDisplayName = ResolveFamilyDisplayNameForMyMachines(familyId);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Family: {familyDisplayName}");
            GUILayout.Label($"ID: {familyId}");

            var hasType = false;
            for (var typeIndex = 0; typeIndex < _savedAircraftDesigns.Count; typeIndex++)
            {
                var savedDesign = _savedAircraftDesigns[typeIndex];
                if (!string.Equals(savedDesign.FamilyResearchId, familyId, StringComparison.Ordinal))
                {
                    continue;
                }

                hasType = true;
                GUILayout.BeginHorizontal(GUI.skin.box);
                var category = AircraftCategoryCatalog.GetProfile(savedDesign.CatalogItem.Category).DisplayName;
                GUILayout.Label(
                    $"{savedDesign.Name} | {category} | {GetAircraftDesignResearchStatus(savedDesign)} | " +
                    $"Approved Year: {FormatApprovedYear(savedDesign.ApprovedYear)}");

                if (GUILayout.Button("Select", GUILayout.Width(90f), GUILayout.Height(24f)))
                {
                    _selectedSavedAircraftDesignIndex = typeIndex;
                }

                if (GUILayout.Button("Type Designer", GUILayout.Width(150f), GUILayout.Height(24f)))
                {
                    OpenTypeDesignerForExistingDesign(typeIndex);
                }

                if (GUILayout.Button("New Version", GUILayout.Width(160f), GUILayout.Height(24f)))
                {
                    BeginTypeVersionDevelopment(typeIndex);
                }

                GUILayout.EndHorizontal();
            }

            if (!hasType)
            {
                GUILayout.Label("No saved types under this family.");
            }

            GUILayout.EndVertical();
        }
    }

    private void DrawMyMachinesSelectedEngineReport()
    {
        DrawSection("Selected Engine Parameters");
        if (_savedEngines.Count == 0)
        {
            GUILayout.Label("No engines saved.");
            return;
        }

        if (_selectedSavedEngineIndex < 0 || _selectedSavedEngineIndex >= _savedEngines.Count)
        {
            _selectedSavedEngineIndex = 0;
        }

        var selected = _savedEngines[_selectedSavedEngineIndex];
        DrawParameterValueRow("Name", selected.Name);
        DrawParameterValueRow("Development Start Year", FormatUiInteger(selected.DevelopmentStartedYear));
        DrawParameterValueRow("Current Variant Start Year", FormatUiInteger(selected.VariantDevelopmentStartedYear));
        DrawParameterValueRow("Approved Year", FormatApprovedYear(selected.ApprovedYear));
        if (TryResolveResearchTimeMonthsFromResearchBoard(ResearchSubjectType.Engine, selected.ResearchId, out var engineResearchTimeMonths))
        {
            DrawParameterValueRow("Engineering Time (months)", FormatUiInteger(engineResearchTimeMonths));
        }

        DrawParameterValueRow("Definition", $"{selected.Definition.DisplayName} ({selected.Definition.EngineType})");
        DrawParameterValueRow("Engine Type", selected.Definition.EngineType.ToString());
        if (IsJetFamily(selected.Definition.EngineType))
        {
            DrawParameterValueRow("Turbine Diameter (cm)", FormatUiNumber(selected.Parameters.TurbineDiameterMeters * 100d, "0"));
            DrawParameterValueRow("Bypass Ratio", FormatUiNumber(selected.Parameters.BypassRatio, "0.00"));
            DrawParameterValueRow("Afterburner", selected.Parameters.HasAfterburner ? "Enabled" : "Disabled");
        }
        else
        {
            DrawParameterValueRow("Cylinder Count", FormatUiInteger(selected.Parameters.CylinderCount));
            DrawParameterValueRow("Displacement (L)", FormatUiNumber(selected.Parameters.DisplacementLiters, "0.0"));
            DrawParameterValueRow("Boost", selected.Parameters.BoostType.ToString());
            DrawParameterValueRow("Injection", selected.Parameters.PistonInjectionType.ToString());
            DrawParameterValueRow("Optimization Height (m)", FormatUiNumber(selected.Parameters.OptimizationAltitudeMeters, "0"));
            DrawParameterValueRow("Efficiency Optimizations (%)", FormatUiNumber(selected.Parameters.EfficiencyOptimizationsPercent, "0.0"));
        }

        DrawParameterValueRow("Compression Ratio", FormatUiNumber(selected.Parameters.CompressionRatio, "0.00"));
        DrawParameterValueRow("Maximum Speed (RPM)", FormatUiNumber(selected.Parameters.MaximumSpeed, "0"));
        DrawParameterValueRow("Cooling Efficiency", FormatUiNumber(selected.Parameters.CoolingEfficiency, "0.00"));

        try
        {
            var built = BuildSavedEngineAtReferenceDate(selected);
            var outputAtSeaLevel = ResolveEngineOutputAtSeaLevel(built);
            if (built.Performance.OutputKind == EngineOutputKind.PowerKilowatts)
            {
                DrawParameterValueRow("Engine Power @0m (kW)", FormatUiNumber(outputAtSeaLevel, "0.0"));
            }
            else
            {
                DrawParameterValueRow("Engine Thrust @0m (N)", FormatUiNumber(outputAtSeaLevel, "0.0"));
            }

            DrawParameterValueRow("Reliability", FormatUiNumber(built.Performance.Reliability, "0.000"));
            DrawParameterValueRow("Weight (kg)", FormatUiNumber(built.Performance.Weight.Kilograms, "0.0"));
            DrawParameterValueRow("Engine Length (m)", FormatUiNumber(built.Performance.EngineLengthMeters, "0.00"));
            DrawParameterValueRow("Engine Frontal Area (m2)", FormatUiNumber(built.Performance.EngineFrontalAreaSquareMeters, "0.000"));
            DrawParameterValueRow("Cost", FormatUiNumber(built.Performance.Cost, "0"));
            DrawParameterValueRow("Production MD", FormatUiNumber(built.Performance.ProductionManDays, "0.0"));
        }
        catch
        {
            DrawError("Selected engine parameters could not be evaluated.");
        }
    }

    private void DrawMyMachinesSelectedTypeReport()
    {
        DrawSection("Selected Type Parameters");
        if (_savedAircraftDesigns.Count == 0)
        {
            GUILayout.Label("No types saved.");
            return;
        }

        if (_selectedSavedAircraftDesignIndex < 0 || _selectedSavedAircraftDesignIndex >= _savedAircraftDesigns.Count)
        {
            _selectedSavedAircraftDesignIndex = 0;
        }

        var selected = _savedAircraftDesigns[_selectedSavedAircraftDesignIndex];
        var aircraft = selected.CatalogItem.Aircraft;
        var category = AircraftCategoryCatalog.GetProfile(selected.CatalogItem.Category).DisplayName;
        DrawParameterValueRow("Name", selected.Name);
        DrawParameterValueRow("Development Start Year", FormatUiInteger(selected.DevelopmentStartedYear));
        DrawParameterValueRow("Current Variant Start Year", FormatUiInteger(selected.VariantDevelopmentStartedYear));
        DrawParameterValueRow("Approved Year", FormatApprovedYear(selected.ApprovedYear));
        if (TryResolveResearchTimeMonthsFromResearchBoard(ResearchSubjectType.AircraftType, selected.TypeResearchId, out var typeResearchTimeMonths))
        {
            DrawParameterValueRow("Engineering Time (months)", FormatUiInteger(typeResearchTimeMonths));
        }
        DrawParameterValueRow("Category", category);
        DrawParameterValueRow(
            "Engine",
            ResolveSavedEngineDisplayNameForAircraftDesign(selected));
        DrawParameterValueRow("Crew Members", FormatUiInteger(aircraft.Airframe.Parameters.CrewCount));
        DrawParameterValueRow("Wing Area (m2)", FormatUiNumber(aircraft.Airframe.Parameters.WingAreaSquareMeters, "0.0"));
        DrawParameterValueRow("Wing Shape", aircraft.Airframe.Parameters.WingShape.ToString());
        DrawParameterValueRow("Wing Profile", aircraft.Airframe.Parameters.WingProfile.ToString());
        DrawParameterValueRow("Tail Type", aircraft.Airframe.Parameters.TailType.ToString());
        DrawParameterValueRow("Flaps", aircraft.Airframe.Parameters.FlapType.ToString());
        DrawParameterValueRow("Leading Edge Flaps", aircraft.Airframe.Parameters.HasLeadingEdgeFlaps ? "Enabled" : "Disabled");
        DrawParameterValueRow("Backup Systems", aircraft.Airframe.Parameters.HasBackupSystems ? "Enabled" : "Disabled");
        DrawParameterValueRow("Landing Gear", aircraft.Airframe.Parameters.LandingGearType.ToString());
        DrawParameterValueRow("Runway Type", aircraft.Airframe.Parameters.RunwayType.ToString());
        DrawParameterValueRow("Cowling", FormatUiNumber(aircraft.Airframe.Parameters.EngineCowling, "0.00"));
        DrawParameterValueRow("Aerodynamic Cleanliness (%)", FormatUiNumber(aircraft.Airframe.Parameters.AerodynamicCleanlinessPercent, "0.0"));
        DrawParameterValueRow("Fuel (L)", FormatUiNumber(aircraft.Airframe.Parameters.FuelAmountLiters, "0"));

        DrawParameterValueRow("Range (km)", FormatUiNumber(aircraft.Performance.RangeKilometers, "0.0"));
        DrawParameterValueRow("Cruise Speed (km/h)", FormatUiNumber(aircraft.Performance.OptimalCruiseSpeedMetersPerSecond * 3.6d, "0.0"));
        DrawParameterValueRow("Useful Weight (kg)", FormatUiNumber(aircraft.Performance.PayloadCapacity.Kilograms, "0.0"));
        DrawParameterValueRow("Overall Reliability", FormatUiNumber(aircraft.Performance.OverallReliability, "0.000"));
        DrawParameterValueRow("Production Cost", FormatUiNumber(aircraft.Performance.TotalProductionCost, "0"));
        DrawParameterValueRow("Production MD", FormatUiNumber(aircraft.Performance.TotalProductionManDays, "0.0"));
    }

    private void ToggleEngineCompareMode()
    {
        if (_engineCompareReferenceIndex >= 0)
        {
            _engineCompareReferenceIndex = -1;
            return;
        }

        if (_savedEngines.Count == 0)
        {
            _engineCompareReferenceIndex = -1;
            return;
        }

        _engineCompareReferenceIndex = _selectedSavedEngineIndex >= 0 && _selectedSavedEngineIndex < _savedEngines.Count
            ? _selectedSavedEngineIndex
            : 0;
    }

    private void ToggleTypeCompareMode()
    {
        if (_typeCompareReferenceIndex >= 0)
        {
            _typeCompareReferenceIndex = -1;
            return;
        }

        if (_savedAircraftDesigns.Count == 0)
        {
            _typeCompareReferenceIndex = -1;
            return;
        }

        _typeCompareReferenceIndex = _selectedSavedAircraftDesignIndex >= 0 && _selectedSavedAircraftDesignIndex < _savedAircraftDesigns.Count
            ? _selectedSavedAircraftDesignIndex
            : 0;
    }

    private void BeginEngineVersionDevelopment(int engineIndex)
    {
        if (engineIndex < 0 || engineIndex >= _savedEngines.Count)
        {
            return;
        }

        var saved = _savedEngines[engineIndex];
        if (!IsEngineDefinitionAvailableForCurrentDate(saved.Definition))
        {
            _enginePreviewError =
                $"Engine '{saved.Definition.DisplayName}' unlocks in {saved.Definition.AvailableFromYear}. Current year is {_currentDate.Year}.";
            return;
        }

        _engineDesignName = $"{saved.Name} v2";
        _engineParameters = saved.Parameters.Clone();
        _selectedEngineDefinitionIndex = FindIndexById(_engineDefinitions.Select(d => d.Id).ToArray(), saved.Definition.Id);
        SyncEngineSelectorsFromDefinition(saved.Definition.EngineType);
        _selectedSavedEngineIndex = engineIndex;
        _engineVersionDevelopment = new EngineVersionDevelopmentContext(
            saved.ResearchId,
            saved.Name,
            saved.Definition.EngineType,
            saved.Parameters.CylinderCount,
            saved.Parameters.Clone(),
            saved.DevelopmentStartedYear,
            _currentDate.Year);
        _typeVersionDevelopment = null;
        RebuildEnginePreview();
        _tab = 0;
        _researchActionMessage =
            $"Developing engine version from '{saved.Name}'. Fixed: engine family/type and cylinder count. " +
            "Engineering quote uses 90% base discount and 50% of technology progress since baseline.";
    }

    private void OpenTypeDesignerForExistingDesign(int designIndex)
    {
        if (!TryLoadSavedAircraftDesignIntoTypeDesigner(designIndex, out var savedDesign, out var errorMessage))
        {
            _factoryActionMessage = errorMessage;
            return;
        }

        _typeVersionDevelopment = null;
        _engineVersionDevelopment = null;
        _aircraftDesignName = savedDesign.Name;
        _tab = 1;
        _factoryActionMessage = $"Loaded type '{savedDesign.Name}' into Airframe + Type Designer.";
    }

    private void BeginTypeVersionDevelopment(int designIndex)
    {
        if (!TryLoadSavedAircraftDesignIntoTypeDesigner(designIndex, out var savedDesign, out var errorMessage))
        {
            _factoryActionMessage = errorMessage;
            return;
        }

        var aircraft = savedDesign.CatalogItem.Aircraft;
        _aircraftDesignName = $"{savedDesign.Name} v2";
        _typeVersionDevelopment = new TypeVersionDevelopmentContext(
            savedDesign.TypeResearchId,
            savedDesign.Name,
            savedDesign.FamilyResearchId,
            aircraft.FamilyDefinition.DisplayName,
            savedDesign.EngineResearchId,
            aircraft.FamilyDefinition,
            savedDesign.DevelopmentStartedYear,
            _currentDate.Year);
        _engineVersionDevelopment = null;
        _tab = 1;
        _factoryActionMessage =
            $"Developing new type version from '{savedDesign.Name}'. " +
            "Engineering quote uses 90% base discount and 50% of technology progress since baseline.";
    }

    private bool TryLoadSavedAircraftDesignIntoTypeDesigner(
        int designIndex,
        out SavedAircraftDesign savedDesign,
        out string errorMessage)
    {
        if (designIndex < 0 || designIndex >= _savedAircraftDesigns.Count)
        {
            savedDesign = null!;
            errorMessage = "Selected type was not found.";
            return false;
        }

        savedDesign = _savedAircraftDesigns[designIndex];
        var aircraft = savedDesign.CatalogItem.Aircraft;
        var familyDefinition = aircraft.FamilyDefinition;
        var typeDefinition = aircraft.TypeDefinition;
        var baseAirframe = familyDefinition.BaseAirframeParameters.Clone();

        _selectedSavedAircraftDesignIndex = designIndex;
        _familyName = familyDefinition.Id;
        _lockedEngineCount = familyDefinition.LockedEngineCount;
        _lockedStructure = familyDefinition.LockedStructureType;
        _lockedMaterial = familyDefinition.LockedMaterialType;
        _familyAllowedEngineFamily = familyDefinition.AllowedEngineFamily == AircraftAllowedEngineFamily.Any
            ? AircraftEngineFamilyCompatibility.GetFamilyForEngineType(familyDefinition.LockedEngineType)
            : familyDefinition.AllowedEngineFamily;
        _selectedAircraftCategory = familyDefinition.Category;
        _baseAirframeParameters = baseAirframe;
        _baseAirframeParameters.EngineCount = familyDefinition.LockedEngineCount;
        _baseAirframeParameters.StructureType = familyDefinition.LockedStructureType;
        _baseAirframeParameters.MaterialType = familyDefinition.LockedMaterialType;

        var airframeIndex = FindIndexById(_airframeDefinitions.Select(definition => definition.Id).ToArray(), familyDefinition.AirframeDefinitionId);
        _selectedAirframeDefinitionIndex = airframeIndex;

        SyncTypeAirframeFromBase();

        var typeDelta = typeDefinition.AirframeDelta;
        _typeCrewCount = Mathf.Clamp(
            baseAirframe.CrewCount + typeDelta.CrewCountDelta,
            PlaneDesignerBalance.Airframe.CrewMin,
            PlaneDesignerBalance.Airframe.CrewMax);
        _typeWingAreaSquareMeters = baseAirframe.WingAreaSquareMeters + typeDelta.WingAreaSquareMetersDelta;
        _typeFuelLiters = baseAirframe.FuelAmountLiters + typeDelta.FuelAmountLitersDelta;
        _typeCowling = Math.Clamp(baseAirframe.EngineCowling + typeDelta.EngineCowlingDelta, 0.1d, 0.9d);
        _typeAerodynamicCleanlinessPercent = Math.Clamp(
            baseAirframe.AerodynamicCleanlinessPercent + typeDelta.AerodynamicCleanlinessPercentDelta,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMinPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent);
        _typeWingShape = typeDelta.WingShapeOverride ?? baseAirframe.WingShape;
        var wingProfile = typeDelta.WingProfileOverride ?? baseAirframe.WingProfile;
        _typeWingProfile = wingProfile == WingProfileType.Standard ? WingProfileType.Laminar6SeriesAerodynamic : wingProfile;
        _typeTailType = typeDelta.TailTypeOverride ?? baseAirframe.TailType;
        _typeFlapType = typeDelta.FlapTypeOverride ?? baseAirframe.FlapType;
        _typeHasLeadingEdgeFlaps = typeDelta.HasLeadingEdgeFlapsOverride ?? baseAirframe.HasLeadingEdgeFlaps;
        _typeHasBackupSystems = typeDelta.HasBackupSystemsOverride ?? baseAirframe.HasBackupSystems;
        _typeLandingGearType = typeDelta.LandingGearTypeOverride ?? baseAirframe.LandingGearType;
        _typeRunwayType = typeDelta.RunwayTypeOverride ?? baseAirframe.RunwayType;

        var sourceEngineIndex = FindSavedEngineIndexByResearchId(savedDesign.EngineResearchId);
        if (sourceEngineIndex >= 0)
        {
            _selectedSavedEngineIndex = sourceEngineIndex;
        }
        else
        {
            errorMessage = $"Source engine for '{savedDesign.Name}' is missing from saved engines.";
            return false;
        }

        RebuildAircraftPreview();
        errorMessage = string.Empty;
        return true;
    }

    private int FindSavedEngineIndexByResearchId(string researchId)
    {
        if (string.IsNullOrWhiteSpace(researchId))
        {
            return -1;
        }

        for (var index = 0; index < _savedEngines.Count; index++)
        {
            if (string.Equals(_savedEngines[index].ResearchId, researchId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private string ResolveSavedEngineDisplayNameForAircraftDesign(SavedAircraftDesign design)
    {
        if (design is null)
        {
            throw new ArgumentNullException(nameof(design));
        }

        var sourceEngineIndex = FindSavedEngineIndexByResearchId(design.EngineResearchId);
        if (sourceEngineIndex >= 0 && sourceEngineIndex < _savedEngines.Count)
        {
            var savedEngine = _savedEngines[sourceEngineIndex];
            return BuildEngineDisplayName(savedEngine.Name, savedEngine.Definition.EngineType, savedEngine.Parameters);
        }

        var engine = design.CatalogItem.Aircraft.Engine;
        return BuildEngineDisplayName(engine.Definition.DisplayName, engine.Definition.EngineType, engine.Parameters);
    }

    private int FindSavedAircraftDesignIndexByTypeResearchId(string typeResearchId)
    {
        if (string.IsNullOrWhiteSpace(typeResearchId))
        {
            return -1;
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            if (string.Equals(_savedAircraftDesigns[index].TypeResearchId, typeResearchId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private string ResolveFamilyDisplayNameForMyMachines(string familyId)
    {
        for (var index = 0; index < _savedFamilies.Count; index++)
        {
            var savedFamily = _savedFamilies[index];
            if (string.Equals(savedFamily.FamilyId, familyId, StringComparison.Ordinal))
            {
                return BuildFamilyDisplayName(
                    savedFamily.Name,
                    savedFamily.LockedEngineCount,
                    savedFamily.LockedMaterialType,
                    savedFamily.LockedStructureType,
                    savedFamily.AllowedEngineFamily);
            }
        }

        for (var index = 0; index < _savedAircraftDesigns.Count; index++)
        {
            var savedDesign = _savedAircraftDesigns[index];
            if (string.Equals(savedDesign.FamilyResearchId, familyId, StringComparison.Ordinal))
            {
                return savedDesign.CatalogItem.Aircraft.FamilyDefinition.DisplayName;
            }
        }

        return familyId;
    }

    private string ResolveResearchStatusLabel(ResearchSubjectType subjectType, string subjectId)
    {
        if (_researchBoard is null)
        {
            return "Engineering board unavailable";
        }

        var projects = _researchBoard.GetAllProjects();
        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            if (project.SubjectType == subjectType
                && string.Equals(project.SubjectId, subjectId, StringComparison.Ordinal))
            {
                return project.Status.ToString();
            }
        }

        return "No proposal";
    }

    private int ResolveResearchReferenceYear(ResearchSubjectType subjectType, string subjectId)
    {
        if (_researchBoard is null || string.IsNullOrWhiteSpace(subjectId))
        {
            return _currentDate.Year;
        }

        var projects = _researchBoard.GetAllProjects();
        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            if (project.SubjectType != subjectType || !string.Equals(project.SubjectId, subjectId, StringComparison.Ordinal))
            {
                continue;
            }

            var monthIndex = project.CompletedMonthIndex ?? project.StartedMonthIndex;
            if (!monthIndex.HasValue)
            {
                break;
            }

            var startDate = ResolveCampaignStartDate();
            return startDate.AddMonths(monthIndex.Value).Year;
        }

        return _currentDate.Year;
    }

    private SimulationDate ResolveCampaignStartDate()
    {
        try
        {
            return _currentDate.AddMonths(-_elapsedMonths);
        }
        catch (ArgumentOutOfRangeException)
        {
            return SimulationDate.WrightTycoonStart;
        }
    }

    private double ApplyEarlyResearchDiscountToCost(double baseCost)
    {
        var multiplier = ResearchBalance.ResolveEarlyResearchCostMultiplier(_currentDate.Year);
        var adjustedCost = System.Math.Max(0d, baseCost * multiplier);
        return System.Math.Round(adjustedCost, 2, MidpointRounding.AwayFromZero);
    }

    private int ResolveEngineResearchTimeMonthsForDesignerPreview(Engine previewEngine, int? versionBaselineYear = null)
    {
        var quote = _researchCalculator.CalculateEngineQuote(previewEngine);
        if (versionBaselineYear.HasValue)
        {
            quote = ApplyVersionDevelopmentQuoteAdjustments(quote, versionBaselineYear.Value);
        }

        quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
        return System.Math.Max(quote.ResearchTimeMonths, 1);
    }

    private int ResolveTypeResearchTimeMonthsForDesignerPreview(Aircraft previewAircraft, int? versionBaselineYear = null)
    {
        var quote = _researchCalculator.CalculateTypeQuote(previewAircraft);
        if (versionBaselineYear.HasValue)
        {
            quote = ApplyVersionDevelopmentQuoteAdjustments(quote, versionBaselineYear.Value);
        }

        quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
        return System.Math.Max(quote.ResearchTimeMonths, 1);
    }

    private ResearchQuote ApplyEarlyResearchDiscountToResearchQuote(ResearchQuote quote) =>
        new(
            ApplyEarlyResearchDiscountToCost(quote.ResearchCost),
            quote.ResearchTimeMonths,
            quote.ComplexityNormalized);

    private ResearchQuote ApplyVersionDevelopmentQuoteAdjustments(ResearchQuote baseQuote, int baselineYear)
    {
        var baseCost = baseQuote.ResearchCost * VersionDevelopmentResearchCostMultiplier;
        var baseTimeMonths = baseQuote.ResearchTimeMonths * VersionDevelopmentResearchTimeMultiplier;
        var technologyProgress = CalculateTechnologyProgressSinceYear(baselineYear);
        var weightedProgress = System.Math.Max(0d, technologyProgress * VersionDevelopmentTechnologyProgressWeight);
        if (weightedProgress > 0d)
        {
            var divisor = 1d + weightedProgress;
            baseCost /= divisor;
            baseTimeMonths /= divisor;
        }

        var adjustedCost = System.Math.Round(System.Math.Max(0d, baseCost), 2, MidpointRounding.AwayFromZero);
        var adjustedTime = System.Math.Max(1, (int)System.Math.Round(baseTimeMonths, MidpointRounding.AwayFromZero));
        return new ResearchQuote(adjustedCost, adjustedTime, baseQuote.ComplexityNormalized);
    }

    private ResearchQuote ApplyVersionDevelopmentAdjustmentsIfNeeded(
        ResearchSubjectType subjectType,
        string subjectId,
        ResearchQuote baseQuote)
    {
        return TryGetVersionDevelopmentBaselineYear(subjectType, subjectId, out var baselineYear)
            ? ApplyVersionDevelopmentQuoteAdjustments(baseQuote, baselineYear)
            : baseQuote;
    }

    private bool TryGetVersionDevelopmentBaselineYear(
        ResearchSubjectType subjectType,
        string subjectId,
        out int baselineYear)
    {
        baselineYear = default;
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        if (subjectType == ResearchSubjectType.Engine)
        {
            for (var index = 0; index < _savedEngines.Count; index++)
            {
                var savedEngine = _savedEngines[index];
                if (!string.Equals(savedEngine.ResearchId, subjectId, StringComparison.Ordinal)
                    || !savedEngine.IsVersionDevelopment)
                {
                    continue;
                }

                baselineYear = savedEngine.DevelopmentStartedYear;
                return true;
            }

            return false;
        }

        if (subjectType == ResearchSubjectType.AircraftType)
        {
            for (var index = 0; index < _savedAircraftDesigns.Count; index++)
            {
                var savedDesign = _savedAircraftDesigns[index];
                if (!string.Equals(savedDesign.TypeResearchId, subjectId, StringComparison.Ordinal)
                    || !savedDesign.IsTypeVersionDevelopment)
                {
                    continue;
                }

                baselineYear = savedDesign.DevelopmentStartedYear;
                return true;
            }
        }

        return false;
    }

    private double CalculateTechnologyProgressSinceYear(int baselineYear)
    {
        var clampedBaselineYear = System.Math.Max(1, baselineYear);
        var baseline = _historicalCampaignTechnologyProgression.ResolveForYear(clampedBaselineYear);
        var current = _historicalCampaignTechnologyProgression.ResolveForDate(_currentDate);
        var engineGain = CalculatePositiveProgressRatio(
            baseline.EnginePerformanceMultiplier,
            current.EnginePerformanceMultiplier);
        var aerodynamicsGain = CalculatePositiveProgressRatio(
            baseline.AerodynamicsEfficiencyMultiplier,
            current.AerodynamicsEfficiencyMultiplier);
        var fuelGain = CalculatePositiveProgressRatio(
            baseline.FuelEfficiencyMultiplier,
            current.FuelEfficiencyMultiplier);

        return (engineGain + aerodynamicsGain + fuelGain) / 3d;
    }

    private static double CalculatePositiveProgressRatio(double baselineMultiplier, double currentMultiplier)
    {
        if (baselineMultiplier <= 0d || currentMultiplier <= baselineMultiplier)
        {
            return 0d;
        }

        return (currentMultiplier / baselineMultiplier) - 1d;
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
        GUILayout.Label("Lines produce continuously until you unassign/remove them, or until an optional total target is reached.");
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
            GUILayout.Label($"{FormatUiInteger(blockedDesignsCount)} saved aircraft types are blocked until engine + family + type engineering is completed.");
        }

        var designOptions = new string[productionReadyDesigns.Length + 1];
        designOptions[0] = "(None)";
        for (var index = 0; index < productionReadyDesigns.Length; index++)
        {
            var design = productionReadyDesigns[index];
            var categoryName = AircraftCategoryCatalog.GetProfile(design.CatalogItem.Category).DisplayName;
            designOptions[index + 1] = $"{design.Name} | {categoryName} | Tool F{FormatUiInteger(design.CatalogItem.RequiredFactoryToolLevel)}";
        }

        if (productionReadyDesigns.Length == 0)
        {
            DrawError("No engineered aircraft types available for production. Approve and complete engineering in Engineering tab.");
        }

        var linesToRemove = new List<string>();
        var lines = _productionFactory.Lines;
        PruneFactoryTotalTargetInputs(lines);
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

            var nextTargetUnits = DrawIntSlider(
                $"Planes / month ({line.LineId})",
                line.MonthlyProductionTargetUnits,
                0,
                FactoryBalance.MaximumAircraftPerLinePerMonth);
            if (nextTargetUnits != line.MonthlyProductionTargetUnits)
            {
                _productionFactory.SetMonthlyProductionTargetUnits(line.LineId, nextTargetUnits);
            }

            if (!_factoryTotalTargetInputByLineId.TryGetValue(line.LineId, out var totalTargetInput))
            {
                totalTargetInput = line.TotalProductionTargetUnits > 0
                    ? FormatUiInteger(line.TotalProductionTargetUnits)
                    : string.Empty;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Total target ({line.LineId})", GUILayout.Width(200f));
            totalTargetInput = GUILayout.TextField(totalTargetInput, GUILayout.Width(120f));
            GUILayout.Label("blank = no limit");
            GUILayout.EndHorizontal();
            _factoryTotalTargetInputByLineId[line.LineId] = totalTargetInput;

            var hasTotalTargetInput = !string.IsNullOrWhiteSpace(totalTargetInput);
            var hasValidTotalTarget = !hasTotalTargetInput;
            var parsedTotalTargetUnits = 0;
            if (hasTotalTargetInput && TryParsePositiveInt(totalTargetInput, out var parsedTarget))
            {
                hasValidTotalTarget = true;
                parsedTotalTargetUnits = parsedTarget;
            }

            if (hasValidTotalTarget)
            {
                var nextTotalTargetUnits = hasTotalTargetInput ? parsedTotalTargetUnits : 0;
                if (nextTotalTargetUnits != line.TotalProductionTargetUnits)
                {
                    _productionFactory.SetTotalProductionTargetUnits(line.LineId, nextTotalTargetUnits);
                }
            }
            else
            {
                DrawError("Total target must be a positive whole number, or leave it blank for no limit.");
            }

            if (line.TotalProductionTargetUnits > 0)
            {
                var remainingUnits = System.Math.Max(line.TotalProductionTargetUnits - line.LifetimeProducedUnits, 0);
                GUILayout.Label(
                    $"Progress: {FormatUiInteger(line.LifetimeProducedUnits)}/{FormatUiInteger(line.TotalProductionTargetUnits)} " +
                    $"(remaining {FormatUiInteger(remainingUnits)})");
            }
            else
            {
                GUILayout.Label($"Progress: {FormatUiInteger(line.LifetimeProducedUnits)} built (no total limit)");
            }

            GUILayout.Label($"Auto-hired staff (last month): {FormatUiInteger(line.Employees)}");

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

    private void PruneFactoryScrapQuantityInputs(IReadOnlyDictionary<string, int> liveWarehouseSnapshot)
    {
        var liveDesignIds = new HashSet<string>(liveWarehouseSnapshot.Keys, StringComparer.Ordinal);
        var staleDesignIds = _factoryScrapQuantityInputByDesignId.Keys
            .Where(designId => !liveDesignIds.Contains(designId))
            .ToArray();
        for (var index = 0; index < staleDesignIds.Length; index++)
        {
            _factoryScrapQuantityInputByDesignId.Remove(staleDesignIds[index]);
        }
    }

    private void PruneFactoryTotalTargetInputs(IReadOnlyList<AircraftProductionLineState> lines)
    {
        var liveLineIds = new HashSet<string>(lines.Select(line => line.LineId), StringComparer.Ordinal);
        var staleLineIds = _factoryTotalTargetInputByLineId.Keys
            .Where(lineId => !liveLineIds.Contains(lineId))
            .ToArray();
        for (var index = 0; index < staleLineIds.Length; index++)
        {
            _factoryTotalTargetInputByLineId.Remove(staleLineIds[index]);
        }
    }

    private void ProcessFactoryScrap(
        string designId,
        string designName,
        int unitsToScrap,
        double unitProductionCost,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> fullProductionCatalog)
    {
        if (_productionFactory is null)
        {
            _factoryActionMessage = "Scrap failed: production factory is not initialized.";
            return;
        }

        if (string.IsNullOrWhiteSpace(designId) || unitsToScrap <= 0)
        {
            _factoryActionMessage = "Scrap failed: invalid design or quantity.";
            return;
        }

        var normalizedDesignName = string.IsNullOrWhiteSpace(designName) ? designId : designName.Trim();
        var normalizedUnitProductionCost = System.Math.Max(unitProductionCost, 0d);
        if (!_productionFactory.TryWithdrawFromWarehouse(designId, unitsToScrap, out var withdrawalMessage))
        {
            _factoryActionMessage = $"Scrap failed: {withdrawalMessage}";
            return;
        }

        var recoveredIncome = normalizedUnitProductionCost * 0.5d * unitsToScrap;
        if (_accountingSystem is not null && recoveredIncome > 0d)
        {
            var inventoryByDesign = _factoryAccountingCalculator.CalculateInventoryValueByDesign(
                _productionFactory.WarehouseUnitsByDesign,
                fullProductionCatalog);
            _accountingSystem.RegisterContractIncome(
                _elapsedMonths,
                _currentDate,
                recoveredIncome,
                inventoryByDesign,
                $"Scrap income: {normalizedDesignName} x{FormatUiInteger(unitsToScrap)}");
        }

        _factoryScrapQuantityInputByDesignId[designId] = "1";
        _factoryActionMessage =
            $"Scrapped {FormatUiInteger(unitsToScrap)} units of '{normalizedDesignName}'. " +
            $"Recovered {FormatCurrency(recoveredIncome)} (50% of production cost, no MD refund).";
    }

    private void DrawFactoryReport()
    {
        DrawSection("Factory Report");
        if (_productionFactory is null)
        {
            DrawError("Production factory is not initialized.");
            return;
        }

        var hasMonthlyReport = _lastFactoryReport is not null;
        if (!hasMonthlyReport)
        {
            GUILayout.Label("No factory month processed yet. Press Next Month.");
        }
        else
        {
            var lastReport = _lastFactoryReport!;
            GUILayout.Label($"Report Date: {FormatSimulationDate(lastReport.Date)}");
            DrawMetric("Factory Tool Level", lastReport.FactoryToolLevel, "0");
            DrawMetric("Produced Aircraft", lastReport.TotalAircraftProduced, "0");
            DrawMetric("Warehouse Aircraft Units", lastReport.TotalWarehouseUnits, "0");
            DrawMetric("Available MD", lastReport.TotalAvailableManDays, "0.0");
            DrawMetric("Used MD", lastReport.TotalUsedManDays, "0.0");
            DrawMetric("Capacity Utilization", lastReport.CapacityUtilization * 100d, "0.0");

            DrawSection("Line Results");
            for (var index = 0; index < lastReport.LineReports.Count; index++)
            {
                var line = lastReport.LineReports[index];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{line.LineId} | {line.AssignedDesignName}");
                GUILayout.Label($"Status: {line.StatusMessage}");
                if (!string.IsNullOrWhiteSpace(line.AssignedDesignId))
                {
                    GUILayout.Label($"Category: {ResolveAircraftCategoryName(line.AssignedDesignId)}");
                }

                GUILayout.Label($"Produced: {FormatUiInteger(line.ProducedUnits)}");
                GUILayout.Label($"Target: {FormatUiInteger(line.TargetUnits)} aircraft / month");
                if (line.TotalProductionTargetUnits > 0)
                {
                    var remainingUnits = System.Math.Max(line.TotalProductionTargetUnits - line.LifetimeProducedUnits, 0);
                    GUILayout.Label(
                        $"Total Target: {FormatUiInteger(line.LifetimeProducedUnits)}/{FormatUiInteger(line.TotalProductionTargetUnits)} " +
                        $"(remaining {FormatUiInteger(remainingUnits)})");
                }
                else
                {
                    GUILayout.Label($"Total Target: no limit | Built: {FormatUiInteger(line.LifetimeProducedUnits)}");
                }
                GUILayout.Label($"Auto Staff: {FormatUiInteger(line.Employees)}");
                GUILayout.Label($"MD: {FormatUiNumber(line.UsedManDays, "0.0")}/{FormatUiNumber(line.AvailableManDays, "0.0")} | Carry: {FormatUiNumber(line.CarryOverManDays, "0.0")}");
                GUILayout.Label($"Capacity: {FormatUiInteger(line.CapacityUnitsAtCurrentStaff)} units");
                GUILayout.Label($"Warehouse Units For Type: {FormatUiInteger(line.WarehouseUnitsAfterProduction)}");
                GUILayout.EndVertical();
            }
        }

        DrawSection("Warehouse Snapshot");
        var liveWarehouseSnapshot = _productionFactory.WarehouseUnitsByDesign;
        var fullProductionCatalog = BuildProductionCatalog(includeOnlyResearched: false);
        PruneFactoryScrapQuantityInputs(liveWarehouseSnapshot);
        if (liveWarehouseSnapshot.Count == 0)
        {
            GUILayout.Label("No aircraft in warehouse.");
        }
        else
        {
            string? pendingScrapDesignId = null;
            string pendingScrapDisplayName = string.Empty;
            int pendingScrapUnits = 0;
            double pendingScrapUnitProductionCost = 0d;

            foreach (var entry in liveWarehouseSnapshot.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal))
            {
                var designName = ResolveAircraftDesignName(entry.Key);
                var categoryName = ResolveAircraftCategoryName(entry.Key);
                var availableUnits = entry.Value;

                if (!fullProductionCatalog.TryGetValue(entry.Key, out var catalogItem))
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"{designName} ({categoryName})");
                    GUILayout.Label($"{FormatUiInteger(availableUnits)} units");
                    DrawError("Scrap unavailable: saved aircraft definition was not found.");
                    GUILayout.EndVertical();
                    continue;
                }

                var unitProductionCost = System.Math.Max(catalogItem.Aircraft.Performance.TotalProductionCost, 0d);
                var unitScrapIncome = unitProductionCost * 0.5d;

                if (!_factoryScrapQuantityInputByDesignId.TryGetValue(entry.Key, out var scrapQuantityInput)
                    || string.IsNullOrWhiteSpace(scrapQuantityInput))
                {
                    scrapQuantityInput = "1";
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{designName} ({categoryName})");
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{FormatUiInteger(availableUnits)} units", GUILayout.Width(120f));
                GUILayout.Label($"Scrap value / unit: {FormatCurrency(unitScrapIncome)}", GUILayout.Width(230f));
                GUILayout.Label("Units", GUILayout.Width(40f));
                scrapQuantityInput = GUILayout.TextField(scrapQuantityInput, GUILayout.Width(90f));
                _factoryScrapQuantityInputByDesignId[entry.Key] = scrapQuantityInput;

                var hasParsedScrapUnits = TryParsePositiveInt(scrapQuantityInput, out var requestedScrapUnits);
                var hasEnoughForRequested = hasParsedScrapUnits && requestedScrapUnits <= availableUnits;

                GUI.enabled = hasEnoughForRequested;
                if (GUILayout.Button("Scrap", GUILayout.Width(90f), GUILayout.Height(24f)))
                {
                    pendingScrapDesignId = entry.Key;
                    pendingScrapDisplayName = designName;
                    pendingScrapUnits = requestedScrapUnits;
                    pendingScrapUnitProductionCost = unitProductionCost;
                }

                GUI.enabled = availableUnits > 0;
                if (GUILayout.Button("Scrap All", GUILayout.Width(110f), GUILayout.Height(24f)))
                {
                    pendingScrapDesignId = entry.Key;
                    pendingScrapDisplayName = designName;
                    pendingScrapUnits = availableUnits;
                    pendingScrapUnitProductionCost = unitProductionCost;
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (hasParsedScrapUnits && requestedScrapUnits > availableUnits)
                {
                    DrawError(
                        $"Requested scrap quantity ({FormatUiInteger(requestedScrapUnits)}) exceeds available units ({FormatUiInteger(availableUnits)}).");
                }

                GUILayout.EndVertical();
            }

            if (!string.IsNullOrWhiteSpace(pendingScrapDesignId) && pendingScrapUnits > 0)
            {
                ProcessFactoryScrap(
                    pendingScrapDesignId,
                    pendingScrapDisplayName,
                    pendingScrapUnits,
                    pendingScrapUnitProductionCost,
                    fullProductionCatalog);
            }
        }

        if (!hasMonthlyReport)
        {
            return;
        }

        DrawSection("Engine Factory");
        var engineReport = _lastFactoryReport!.EngineFactoryReport;
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
        var activeTypeVersionDevelopment = _typeVersionDevelopment;
        var isTypeVersionDevelopment = activeTypeVersionDevelopment is not null;
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

        SavedEngineDesign selectedSavedEngine;
        if (isTypeVersionDevelopment)
        {
            var sourceEngineIndex = FindSavedEngineIndexByResearchId(activeTypeVersionDevelopment!.SourceEngineResearchId);
            if (sourceEngineIndex < 0 || sourceEngineIndex >= _savedEngines.Count)
            {
                _aircraftPreviewError = "Source engine for this type version is missing.";
                return;
            }

            _selectedSavedEngineIndex = sourceEngineIndex;
            selectedSavedEngine = _savedEngines[sourceEngineIndex];
        }
        else if (!TryGetSelectedCompatibleSavedEngine(out selectedSavedEngine, out var engineSelectionError))
        {
            _aircraftPreviewError = engineSelectionError;
            return;
        }

        var name = string.IsNullOrWhiteSpace(_aircraftDesignName)
            ? $"{_aircraftPreview.FamilyDefinition.DisplayName} {_aircraftPreview.TypeDefinition.DisplayName}"
            : _aircraftDesignName.Trim();

        var familyResearchId = isTypeVersionDevelopment
            ? activeTypeVersionDevelopment!.SourceFamilyDefinition.Id
            : _aircraftPreview.FamilyDefinition.Id;
        var baseId = $"{familyResearchId}.{BuildTypeId()}";
        var uniqueId = CreateUniqueAircraftDesignId(baseId);
        var typeResearchId = $"type.{uniqueId}";

        if (_aircraftFactory is null)
        {
            _aircraftPreviewError = "Aircraft factory is not initialized.";
            return;
        }

        var typeModifiers = GetResearchModifierPoints(ResearchSubjectType.AircraftType, typeResearchId);
        var typeDefinition = BuildType(familyResearchId, typeModifiers);
        var familyDefinitionForCatalog = isTypeVersionDevelopment
            ? activeTypeVersionDevelopment!.SourceFamilyDefinition
            : _aircraftPreview.FamilyDefinition;
        var aircraftForCatalog = _aircraftFactory.CreateAircraft(familyDefinitionForCatalog, typeDefinition);

        var catalogItem = new FactoryAircraftTypeCatalogItem(
            uniqueId,
            name,
            aircraftForCatalog,
            aircraftForCatalog.FamilyDefinition.RequiredFactoryToolLevel,
            1);

        int? approvedYear = null;
        int? frozenYear = null;
        int? frozenMonth = null;
        if (isTypeVersionDevelopment && !string.IsNullOrWhiteSpace(activeTypeVersionDevelopment!.SourceTypeResearchId))
        {
            var sourceTypeIndex = FindSavedAircraftDesignIndexByTypeResearchId(activeTypeVersionDevelopment.SourceTypeResearchId);
            if (sourceTypeIndex >= 0 && sourceTypeIndex < _savedAircraftDesigns.Count)
            {
                approvedYear = _savedAircraftDesigns[sourceTypeIndex].ApprovedYear;
                frozenYear = _savedAircraftDesigns[sourceTypeIndex].FrozenYear;
                frozenMonth = _savedAircraftDesigns[sourceTypeIndex].FrozenMonth;
            }
        }

        var savedDesign = new SavedAircraftDesign(
            name,
            catalogItem,
            selectedSavedEngine.ResearchId,
            familyResearchId,
            typeResearchId,
            activeTypeVersionDevelopment?.SourceDevelopmentStartedYear ?? _currentDate.Year,
            activeTypeVersionDevelopment?.VariantDevelopmentStartedYear ?? _currentDate.Year,
            activeTypeVersionDevelopment?.SourceDevelopmentStartedYear,
            activeTypeVersionDevelopment?.SourceTypeResearchId ?? string.Empty,
            approvedYear,
            frozenYear,
            frozenMonth);
        _savedAircraftDesigns.Add(savedDesign);
        _selectedSavedAircraftDesignIndex = _savedAircraftDesigns.Count - 1;

        if (!isTypeVersionDevelopment)
        {
            RegisterFamilyResearchProposal(aircraftForCatalog.FamilyDefinition.Id, aircraftForCatalog.FamilyDefinition.DisplayName, aircraftForCatalog);
        }

        RegisterTypeResearchProposal(typeResearchId, aircraftForCatalog.TypeDefinition.DisplayName, aircraftForCatalog);

        if (isTypeVersionDevelopment)
        {
            _typeVersionDevelopment = null;
            _factoryActionMessage =
                $"Saved type version '{name}' ({AircraftCategoryCatalog.GetProfile(catalogItem.Category).DisplayName}). " +
                "Type research was submitted with version-development pricing and awaits approval/completion.";
        }
        else
        {
            _factoryActionMessage =
                $"Saved aircraft design '{name}' ({AircraftCategoryCatalog.GetProfile(catalogItem.Category).DisplayName}) for production. " +
                "Type is blocked until engine/family/type research is approved and completed.";
        }
    }

    private void SaveCurrentFamily()
    {
        if (_lockedMaterial == MaterialType.Composite && !IsCompositeMaterialUnlockedForCurrentState())
        {
            _factoryActionMessage =
                $"Composite material is locked until event '{PlaneDesignerBalance.Airframe.CompositeMaterialUnlockEventId}'.";
            return;
        }

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
        if (_lockedMaterial == MaterialType.Composite && !IsCompositeMaterialUnlockedForCurrentState())
        {
            _lockedMaterial = MaterialType.Aluminum;
        }

        _baseAirframeParameters.WingPosition = saved.WingPosition;
        _baseAirframeParameters.EngineCount = saved.LockedEngineCount;
        _baseAirframeParameters.StructureType = saved.LockedStructureType;
        _baseAirframeParameters.MaterialType = _lockedMaterial;
        _familyAllowedEngineFamily = saved.AllowedEngineFamily;
        _selectedSavedFamilyIndex = index;
        _factoryActionMessage = $"Loaded family '{BuildFamilyDisplayName(saved.Name, saved.LockedEngineCount, _lockedMaterial, saved.LockedStructureType, saved.AllowedEngineFamily)}'.";
    }

    private void ResetFamilyConfig()
    {
        _lockedEngineCount = NormalizeEngineCount(_defaultEngineCount);
        _lockedStructure = StructureType.Truss;
        _lockedMaterial = MaterialType.WoodFabric;
        _familyAllowedEngineFamily = AircraftAllowedEngineFamily.Propeller;
        _baseAirframeParameters = CreateDefaultAirframeParameters(_lockedEngineCount);
    }

    private string CreateUniqueAircraftDesignId(string baseId)
    {
        var cleanBase = string.IsNullOrWhiteSpace(baseId) ? "aircraft.design" : baseId;
        var candidate = cleanBase;
        var suffix = 1;
        while (IsAircraftDesignIdInUse(candidate))
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
        while (IsEngineResearchIdInUse(candidate))
        {
            candidate = $"{baseId}.{suffix}";
            suffix++;
        }

        return candidate;
    }

    private bool IsAircraftDesignIdInUse(string designId)
    {
        if (_savedAircraftDesigns.Any(design => string.Equals(design.CatalogItem.DesignId, designId, StringComparison.Ordinal)))
        {
            return true;
        }

        var typeResearchSubjectId = $"type.{designId}";
        return HasResearchProjectSubject(ResearchSubjectType.AircraftType, typeResearchSubjectId);
    }

    private bool IsEngineResearchIdInUse(string researchId)
    {
        if (_savedEngines.Any(engine => string.Equals(engine.ResearchId, researchId, StringComparison.Ordinal)))
        {
            return true;
        }

        return HasResearchProjectSubject(ResearchSubjectType.Engine, researchId);
    }

    private bool HasResearchProjectSubject(ResearchSubjectType subjectType, string subjectId)
    {
        if (_researchBoard is null || string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        var researchId = ResearchBoard.BuildResearchId(subjectType, subjectId);
        var projects = _researchBoard.GetAllProjects();
        for (var index = 0; index < projects.Count; index++)
        {
            if (string.Equals(projects[index].ResearchId, researchId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
        quote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.Engine, savedDesign.ResearchId, quote);
        quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
        _researchBoard.UpsertProposal(
            ResearchSubjectType.Engine,
            savedDesign.ResearchId,
            engineDisplayName,
            quote);
        if (savedDesign.IsVersionDevelopment)
        {
            _researchActionMessage =
                $"Engine version '{engineDisplayName}' submitted for approval: {FormatCurrency(quote.ResearchCost)} / {FormatUiInteger(quote.ResearchTimeMonths)} months.";
        }
        else
        {
            _researchActionMessage =
                $"Engine '{engineDisplayName}' submitted for approval: {FormatCurrency(quote.ResearchCost)} / {FormatUiInteger(quote.ResearchTimeMonths)} months.";
        }
    }

    private void RegisterFamilyResearchProposal(string familyId, string familyName, Aircraft aircraft)
    {
        if (_researchBoard is null)
        {
            return;
        }

        var points = GetResearchModifierPoints(ResearchSubjectType.AircraftFamily, familyId);
        var quote = _researchCalculator.CalculateFamilyQuote(aircraft, points);
        quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
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
        quote = ApplyVersionDevelopmentAdjustmentsIfNeeded(ResearchSubjectType.AircraftType, typeId, quote);
        quote = ApplyEarlyResearchDiscountToResearchQuote(quote);
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
            return "Engineering board unavailable";
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

    private void PruneContractDeliverySelections(
        IReadOnlyList<Contract> openContracts,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem>? catalog = null)
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

        var staleExclusionKeys = _excludedWarehouseDesignByContractId.Keys
            .Where(contractId => !openIds.Contains(contractId))
            .ToArray();
        for (var index = 0; index < staleExclusionKeys.Length; index++)
        {
            _excludedWarehouseDesignByContractId.Remove(staleExclusionKeys[index]);
        }

        if (catalog is null)
        {
            return;
        }

        var validDesignIds = new HashSet<string>(catalog.Keys, StringComparer.Ordinal);
        var openContractIds = openIds.ToArray();
        for (var index = 0; index < openContractIds.Length; index++)
        {
            var contractId = openContractIds[index];
            if (_selectedWarehouseDesignByContractId.TryGetValue(contractId, out var selectedDesignId)
                && !validDesignIds.Contains(selectedDesignId))
            {
                _selectedWarehouseDesignByContractId.Remove(contractId);
            }

            if (!_excludedWarehouseDesignByContractId.TryGetValue(contractId, out var excludedDesignIds))
            {
                continue;
            }

            excludedDesignIds.IntersectWith(validDesignIds);
            if (excludedDesignIds.Count == 0)
            {
                _excludedWarehouseDesignByContractId.Remove(contractId);
            }
        }
    }

    private void PruneGovernmentDeliveryQuantityInputs(IReadOnlyList<GovernmentDeliveryContract> activeDeliveries)
    {
        var activeDeliveryIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < activeDeliveries.Count; index++)
        {
            activeDeliveryIds.Add(activeDeliveries[index].DeliveryId);
        }

        var staleKeys = _governmentDeliveryQuantityInputById.Keys
            .Where(deliveryId => !activeDeliveryIds.Contains(deliveryId))
            .ToArray();
        for (var index = 0; index < staleKeys.Length; index++)
        {
            _governmentDeliveryQuantityInputById.Remove(staleKeys[index]);
        }
    }

    private ContractAvailabilitySnapshot BuildContractAvailabilitySnapshot(
        Contract contract,
        ContractBoard contractBoard,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog,
        IReadOnlyDictionary<string, int> warehouseSnapshot)
    {
        var hasMatchingPlaneBySpecification = false;
        var deliverableWarehouseUnits = 0;

        foreach (var catalogItem in catalog.Values)
        {
            if (!IsCatalogItemCategoryCompatibleWithContract(contract, catalogItem))
            {
                continue;
            }

            var evaluation = contractBoard.Evaluate(contract, catalogItem.Aircraft);
            if (!evaluation.CanDeliver)
            {
                continue;
            }

            hasMatchingPlaneBySpecification = true;
            if (warehouseSnapshot.TryGetValue(catalogItem.DesignId, out var stockUnits)
                && stockUnits > 0)
            {
                deliverableWarehouseUnits += stockUnits;
                if (deliverableWarehouseUnits >= contract.QuantityUnits)
                {
                    break;
                }
            }
        }

        var canBeDeliveredNow = deliverableWarehouseUnits >= contract.QuantityUnits;
        return new ContractAvailabilitySnapshot(contract, canBeDeliveredNow, hasMatchingPlaneBySpecification);
    }

    private IReadOnlyList<ContractDeliveryCandidate> BuildContractDeliveryCandidates(
        Contract contract,
        ContractBoard contractBoard,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog,
        IReadOnlyDictionary<string, int> warehouseSnapshot)
    {
        var candidates = new List<ContractDeliveryCandidate>();
        foreach (var warehouseEntry in warehouseSnapshot)
        {
            if (warehouseEntry.Value <= 0 || !catalog.TryGetValue(warehouseEntry.Key, out var catalogItem))
            {
                continue;
            }

            if (!IsCatalogItemCategoryCompatibleWithContract(contract, catalogItem))
            {
                continue;
            }

            var evaluation = contractBoard.Evaluate(contract, catalogItem.Aircraft);
            if (!evaluation.CanDeliver)
            {
                continue;
            }

            candidates.Add(new ContractDeliveryCandidate(
                warehouseEntry.Key,
                catalogItem,
                warehouseEntry.Value,
                CalculateAircraftUnitCostWithManDayLabor(catalogItem.Aircraft)));
        }

        return candidates
            .OrderBy(candidate => candidate.UnitFulfillmentCost)
            .ThenBy(candidate => candidate.CatalogItem.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.DesignId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ContractDeliveryAllocation> BuildCheapestContractDeliveryPlan(
        int requiredUnits,
        IReadOnlyList<ContractDeliveryCandidate> candidates)
    {
        var deliveryPlan = new List<ContractDeliveryAllocation>();
        var unitsRemaining = System.Math.Max(requiredUnits, 0);
        for (var index = 0; index < candidates.Count && unitsRemaining > 0; index++)
        {
            var candidate = candidates[index];
            var unitsFromCandidate = System.Math.Min(candidate.StockUnits, unitsRemaining);
            if (unitsFromCandidate <= 0)
            {
                continue;
            }

            deliveryPlan.Add(new ContractDeliveryAllocation(candidate, unitsFromCandidate));
            unitsRemaining -= unitsFromCandidate;
        }

        return unitsRemaining == 0
            ? deliveryPlan
            : Array.Empty<ContractDeliveryAllocation>();
    }

    private static string BuildContractDeliveryPlanSummary(IReadOnlyList<ContractDeliveryAllocation> deliveryPlan)
    {
        var parts = new string[deliveryPlan.Count];
        for (var index = 0; index < deliveryPlan.Count; index++)
        {
            var allocation = deliveryPlan[index];
            parts[index] = $"{FormatUiInteger(allocation.UnitsToDeliver)}x {allocation.Candidate.CatalogItem.DisplayName}";
        }

        return string.Join(", ", parts);
    }

    private static bool IsCatalogItemCategoryCompatibleWithContract(
        Contract contract,
        FactoryAircraftTypeCatalogItem catalogItem)
    {
        if (contract is null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        if (catalogItem is null)
        {
            throw new ArgumentNullException(nameof(catalogItem));
        }

        if (!contract.Requirements.RequiredCategory.HasValue)
        {
            return true;
        }

        return catalogItem.Category == contract.Requirements.RequiredCategory.Value;
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

        if (_contractRequestedCategoryFilterEnabled)
        {
            if (!snapshot.Contract.Requirements.RequiredCategory.HasValue)
            {
                return false;
            }

            if (snapshot.Contract.Requirements.RequiredCategory.Value != _contractRequestedCategoryFilter)
            {
                return false;
            }
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
        LogHistoricalCampaignTechnologyModifiersIfYearChanged();
        var historicalCampaignBonusBatches = ProcessHistoricalCampaignForCurrentDate();

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
            var generatedHistoricalBonusContracts = InjectHistoricalCampaignBonusContractsForCurrentMonth(historicalCampaignBonusBatches);
            _lastGeneratedContracts.Clear();
            _lastGeneratedContracts.AddRange(generated);
            if (generatedHistoricalBonusContracts.Count > 0)
            {
                _lastGeneratedContracts.AddRange(generatedHistoricalBonusContracts);
            }

            var canceledDelta = _contractBoard.CanceledContracts.Count - canceledBefore;
            var totalGeneratedContracts = generated.Count + generatedHistoricalBonusContracts.Count;
            if (generatedHistoricalBonusContracts.Count > 0)
            {
                _contractActionMessage =
                    $"Advanced to {FormatSimulationDate(_currentDate)}. Generated {FormatUiInteger(totalGeneratedContracts)} contracts " +
                    $"({FormatUiInteger(generated.Count)} regular + {FormatUiInteger(generatedHistoricalBonusContracts.Count)} historical bonus), " +
                    $"canceled {FormatUiInteger(canceledDelta)}.";
            }
            else
            {
                _contractActionMessage =
                    $"Advanced to {FormatSimulationDate(_currentDate)}. Generated {FormatUiInteger(generated.Count)} contracts, canceled {FormatUiInteger(canceledDelta)}.";
            }

            LogGeneratedContractMix(_lastGeneratedContracts);
        }

        if (_governmentContractBoard is not null)
        {
            var governmentUpdate = _governmentContractBoard.ProcessMonth(_currentDate);
            if (governmentUpdate.Events.Count > 0)
            {
                _governmentContractActionMessage = string.Join(" ", governmentUpdate.Events);
            }

            if (governmentUpdate.GeneratedContract is not null)
            {
                OpenGovernmentContractNotebookWindow();
            }
            else if (governmentUpdate.EvaluationResult is not null || governmentUpdate.ExpiredContract is not null)
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

            if (_governmentContractBoard is not null)
            {
                PruneGovernmentDeliveryQuantityInputs(_governmentContractBoard.ActiveDeliveryContracts);
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

        NormalizeEngineSelectionForCurrentDate();
        if (_selectedEngineDefinitionIndex < 0 || _selectedEngineDefinitionIndex >= _engineDefinitions.Count)
        {
            return;
        }

        var selectedDefinition = _engineDefinitions[_selectedEngineDefinitionIndex];
        if (!IsEngineDefinitionAvailableForCurrentDate(selectedDefinition))
        {
            _enginePreviewError =
                $"Engine '{selectedDefinition.DisplayName}' unlocks in {selectedDefinition.AvailableFromYear}. Current year is {_currentDate.Year}.";
            return;
        }

        try
        {
            _enginePreview = _engineBuilder.Build(selectedDefinition, _engineParameters);
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
            SavedEngineDesign saved;
            if (_typeVersionDevelopment is not null)
            {
                var sourceEngineIndex = FindSavedEngineIndexByResearchId(_typeVersionDevelopment.SourceEngineResearchId);
                if (sourceEngineIndex < 0 || sourceEngineIndex >= _savedEngines.Count)
                {
                    _aircraftPreviewError = "Source engine for this type version is missing.";
                    return;
                }

                _selectedSavedEngineIndex = sourceEngineIndex;
                saved = _savedEngines[sourceEngineIndex];
            }
            else if (!TryGetSelectedCompatibleSavedEngine(out saved, out var engineSelectionError))
            {
                _aircraftPreviewError = engineSelectionError;
                return;
            }

            var family = _typeVersionDevelopment is not null
                ? _typeVersionDevelopment.SourceFamilyDefinition
                : BuildFamily(saved);
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

        var activeVersionDevelopment = _engineVersionDevelopment;
        var name = string.IsNullOrWhiteSpace(_engineDesignName)
            ? $"Engine-{_savedEngines.Count + 1}"
            : _engineDesignName.Trim();
        var researchId = CreateUniqueEngineResearchId(name);

        int? approvedYear = null;
        int? frozenYear = null;
        int? frozenMonth = null;
        if (activeVersionDevelopment is not null && !string.IsNullOrWhiteSpace(activeVersionDevelopment.SourceResearchId))
        {
            var sourceEngineIndex = FindSavedEngineIndexByResearchId(activeVersionDevelopment.SourceResearchId);
            if (sourceEngineIndex >= 0 && sourceEngineIndex < _savedEngines.Count)
            {
                approvedYear = _savedEngines[sourceEngineIndex].ApprovedYear;
                frozenYear = _savedEngines[sourceEngineIndex].FrozenYear;
                frozenMonth = _savedEngines[sourceEngineIndex].FrozenMonth;
            }
        }

        var savedDesign = new SavedEngineDesign(
            name,
            researchId,
            _enginePreview,
            activeVersionDevelopment?.SourceDevelopmentStartedYear ?? _currentDate.Year,
            activeVersionDevelopment?.VariantDevelopmentStartedYear ?? _currentDate.Year,
            activeVersionDevelopment?.SourceDevelopmentStartedYear,
            activeVersionDevelopment?.SourceResearchId ?? string.Empty,
            approvedYear,
            frozenYear,
            frozenMonth);
        _savedEngines.Add(savedDesign);
        _selectedSavedEngineIndex = _savedEngines.Count - 1;

        RegisterEngineResearchProposal(savedDesign, _enginePreview);
        if (activeVersionDevelopment is not null)
        {
            _engineVersionDevelopment = null;
        }
    }

    private void LoadSavedEngine(int index)
    {
        if (index < 0 || index >= _savedEngines.Count)
        {
            return;
        }

        var saved = _savedEngines[index];
        if (!IsEngineDefinitionAvailableForCurrentDate(saved.Definition))
        {
            _enginePreviewError =
                $"Engine '{saved.Definition.DisplayName}' unlocks in {saved.Definition.AvailableFromYear}. Current year is {_currentDate.Year}.";
            return;
        }

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
            var savedEngine = _savedEngines[index];
            if (!IsEngineDefinitionAvailableForCurrentDate(savedEngine.Definition))
            {
                continue;
            }

            var engineType = savedEngine.Definition.EngineType;
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
            BaseDevelopmentCost = ApplyEarlyResearchDiscountToCost(_baseDevelopmentCost),
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

    private static string ResolveHistoricalCampaignPath()
    {
        var streamingPath = Path.Combine(Application.streamingAssetsPath, "Data", "Campaign", "events.json");
        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var rootPath = Path.Combine(projectRoot, "Data", "Campaign", "events.json");
        if (File.Exists(rootPath))
        {
            return rootPath;
        }

        throw new FileNotFoundException("Historical campaign JSON file was not found in StreamingAssets or Data/Campaign.");
    }

    private IReadOnlyList<HistoricalCampaignBonusBatchRequest> ProcessHistoricalCampaignForCurrentDate()
    {
        if (_historicalCampaign is null)
        {
            return Array.Empty<HistoricalCampaignBonusBatchRequest>();
        }

        var result = _historicalCampaign.TryTrigger(_currentDate, _historicalCampaignState);
        if (!result.HasTriggeredEvent)
        {
            return Array.Empty<HistoricalCampaignBonusBatchRequest>();
        }

        var triggeredEvent = result.TriggeredEvent!;
        AppendHistoricalCampaignArchiveEntry(triggeredEvent);
        var image = _historicalCampaignImageLoader.LoadForTitle(triggeredEvent.Title);
        _historicalCampaignPopupWindow.Show(triggeredEvent, image);
        Debug.Log($"[Campaign] Popup shown: {triggeredEvent.Id}");

        var bonusBatchRequests = BuildHistoricalCampaignBonusBatchRequests(triggeredEvent);
        if (bonusBatchRequests.Count > 0)
        {
            var totalBonusContracts = 0;
            for (var index = 0; index < bonusBatchRequests.Count; index++)
            {
                totalBonusContracts += bonusBatchRequests[index].RequestedContracts;
            }

            Debug.Log(
                $"[Campaign] Queued {bonusBatchRequests.Count} bonus contract batch(es), total requested contracts={totalBonusContracts}, event={triggeredEvent.Id}.");
        }

        _campaignActionMessage =
            $"Historical event triggered: {triggeredEvent.Title} ({FormatSimulationDate(triggeredEvent.GetDate())}).";
        Debug.Log(
            $"[Campaign] Event fired: {triggeredEvent.Id} | {triggeredEvent.Title} | {triggeredEvent.Year:D4}-{triggeredEvent.Month:D2}");
        LogHistoricalCampaignContractModifiers();
        return bonusBatchRequests;
    }

    private List<HistoricalCampaignBonusBatchRequest> BuildHistoricalCampaignBonusBatchRequests(HistoricalCampaignEventDefinition triggeredEvent)
    {
        var bonusBatchRequests = new List<HistoricalCampaignBonusBatchRequest>();
        if (triggeredEvent.Effects is null || triggeredEvent.Effects.Count == 0)
        {
            return bonusBatchRequests;
        }

        var targetMonthIndex = _contractBoard is null
            ? _elapsedMonths
            : _contractBoard.CurrentMonthIndex + 1;
        var militaryWeightMultiplier = ResolveHistoricalCampaignMilitaryWeightMultiplier();

        for (var effectIndex = 0; effectIndex < triggeredEvent.Effects.Count; effectIndex++)
        {
            var effect = triggeredEvent.Effects[effectIndex];
            if (effect is null)
            {
                continue;
            }

            if (!string.Equals(effect.Type, HistoricalCampaignBonusContractBatchEffectType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ContractBalance.TryResolveHistoricalCampaignBonusBatchRange(effect.BalanceKey, out var minContracts, out var maxContracts))
            {
                Debug.LogWarning(
                    $"[Campaign] Bonus contract batch skipped for event '{triggeredEvent.Id}'. Unknown balance key '{effect.BalanceKey}'.");
                continue;
            }

            var requestedContracts = ResolveDeterministicBonusContractCount(
                minContracts,
                maxContracts,
                triggeredEvent.Id,
                targetMonthIndex,
                effectIndex);
            if (requestedContracts <= 0)
            {
                continue;
            }

            bonusBatchRequests.Add(new HistoricalCampaignBonusBatchRequest(
                triggeredEvent.Id,
                triggeredEvent.Title,
                effect.BalanceKey?.Trim() ?? string.Empty,
                effectIndex,
                targetMonthIndex,
                requestedContracts,
                militaryWeightMultiplier));
        }

        return bonusBatchRequests;
    }

    private IReadOnlyList<Contract> InjectHistoricalCampaignBonusContractsForCurrentMonth(
        IReadOnlyList<HistoricalCampaignBonusBatchRequest> bonusBatchRequests)
    {
        if (_contractBoard is null || bonusBatchRequests is null || bonusBatchRequests.Count == 0)
        {
            return Array.Empty<Contract>();
        }

        var monthIndex = _contractBoard.CurrentMonthIndex;
        var bonusContracts = new List<Contract>(capacity: 32);
        for (var batchIndex = 0; batchIndex < bonusBatchRequests.Count; batchIndex++)
        {
            var request = bonusBatchRequests[batchIndex];
            if (request.TargetMonthIndex != monthIndex)
            {
                Debug.LogWarning(
                    $"[Campaign] Bonus batch target month mismatch for event '{request.EventId}'. " +
                    $"Expected {request.TargetMonthIndex}, current board month is {monthIndex}. Skipping.");
                continue;
            }

            var categories = ContractBalance.BuildBonusBatchRequestedCategories(request.MilitaryWeightMultiplier);
            var options = new ContractGeneratorOptions
            {
                MinContractsPerMonth = request.RequestedContracts,
                MaxContractsPerMonth = request.RequestedContracts,
                RequestedCategories = categories,
            };

            var seed = ComputeHistoricalCampaignBonusSeed(request.EventId, monthIndex, request.EffectIndex);
            var generator = new ContractGenerator(
                seed,
                options,
                _historicalCampaignContractModifierSource,
                () => _currentDate);
            var generatedBatch = generator.GenerateMonthlyContracts(monthIndex);
            var remappedBatch = RemapHistoricalCampaignBonusContractIds(
                generatedBatch,
                request,
                monthIndex,
                bonusContracts.Count);
            bonusContracts.AddRange(remappedBatch);

            Debug.Log(
                $"[Campaign] Bonus batch generated: event={request.EventId}, title={request.EventTitle}, key={request.BalanceKey}, " +
                $"requested={request.RequestedContracts}, produced={remappedBatch.Count}, militaryWeight={request.MilitaryWeightMultiplier:F3}, seed={seed}.");
        }

        if (bonusContracts.Count == 0)
        {
            return Array.Empty<Contract>();
        }

        var injected = _contractBoard.InjectGeneratedContractsForCurrentMonth(bonusContracts);
        Debug.Log($"[Campaign] Injected {injected} historical bonus contract(s) for {FormatSimulationDate(_currentDate)}.");
        return bonusContracts;
    }

    private static IReadOnlyList<Contract> RemapHistoricalCampaignBonusContractIds(
        IReadOnlyList<Contract> generatedBatch,
        HistoricalCampaignBonusBatchRequest request,
        int monthIndex,
        int sequenceOffset)
    {
        var remappedBatch = new List<Contract>(generatedBatch.Count);
        for (var generatedIndex = 0; generatedIndex < generatedBatch.Count; generatedIndex++)
        {
            var source = generatedBatch[generatedIndex];
            var remappedId = BuildHistoricalCampaignBonusContractId(
                request.EventId,
                monthIndex,
                request.EffectIndex,
                sequenceOffset + generatedIndex);
            var remappedContract = new Contract(
                remappedId,
                source.CompanyName,
                source.QuantityUnits,
                source.DeadlineMonths,
                CloneContractRequirements(source.Requirements),
                source.CreatedMonthIndex,
                source.UnitPrice,
                source.EstimatedMinimumUnitCost,
                source.AppliedMarkup);
            remappedBatch.Add(remappedContract);
        }

        return remappedBatch;
    }

    private static ContractRequirements CloneContractRequirements(ContractRequirements requirements)
    {
        ContractSpeedRequirement? minimumSpeed = null;
        if (requirements.MinimumSpeed.HasValue)
        {
            minimumSpeed = new ContractSpeedRequirement(
                requirements.MinimumSpeed.Value.AltitudeMeters,
                requirements.MinimumSpeed.Value.MinimumSpeedMetersPerSecond);
        }

        var clone = new ContractRequirements
        {
            RequiredCategory = requirements.RequiredCategory,
            MinimumReliability = requirements.MinimumReliability,
            MaximumTotalWeightKilograms = requirements.MaximumTotalWeightKilograms,
            MinimumRangeKilometers = requirements.MinimumRangeKilometers,
            MinimumSpeed = minimumSpeed,
            MinimumPayloadKilograms = requirements.MinimumPayloadKilograms,
            MinimumManeuverability = requirements.MinimumManeuverability,
            MinimumCrew = requirements.MinimumCrew,
            MaximumRequiredRunwayLengthMeters = requirements.MaximumRequiredRunwayLengthMeters,
            MaximumFuelConsumptionKilogramsPerHour = requirements.MaximumFuelConsumptionKilogramsPerHour,
        };
        clone.Validate();
        return clone;
    }

    private double ResolveHistoricalCampaignMilitaryWeightMultiplier()
    {
        if (_historicalCampaignContractModifierSource is null)
        {
            return 1d;
        }

        var militaryWeightMultiplier = _historicalCampaignContractModifierSource
            .GetCurrentModifiers()
            .MilitaryCategoryWeightMultiplier;
        if (double.IsNaN(militaryWeightMultiplier)
            || double.IsInfinity(militaryWeightMultiplier)
            || militaryWeightMultiplier <= 0d)
        {
            return 1d;
        }

        return militaryWeightMultiplier;
    }

    private static int ResolveDeterministicBonusContractCount(
        int minContracts,
        int maxContracts,
        string eventId,
        int monthIndex,
        int effectIndex)
    {
        if (minContracts > maxContracts)
        {
            (minContracts, maxContracts) = (maxContracts, minContracts);
        }

        if (minContracts == maxContracts)
        {
            return minContracts;
        }

        var range = maxContracts - minContracts + 1;
        var seed = ComputeHistoricalCampaignBonusSeed(eventId, monthIndex, effectIndex);
        return minContracts + (seed % range);
    }

    private static int ComputeHistoricalCampaignBonusSeed(string? eventId, int monthIndex, int effectIndex)
    {
        unchecked
        {
            uint hash = 2166136261;
            var normalizedEventId = string.IsNullOrWhiteSpace(eventId)
                ? "historical_event"
                : eventId.Trim();
            for (var index = 0; index < normalizedEventId.Length; index++)
            {
                hash ^= normalizedEventId[index];
                hash *= 16777619;
            }

            hash ^= (uint)monthIndex;
            hash *= 16777619;
            hash ^= (uint)effectIndex;
            hash *= 16777619;

            var seed = (int)(hash & 0x7FFFFFFF);
            return seed == 0 ? 1 : seed;
        }
    }

    private static string BuildHistoricalCampaignBonusContractId(string eventId, int monthIndex, int effectIndex, int sequence)
    {
        var normalizedEventId = string.IsNullOrWhiteSpace(eventId)
            ? "event"
            : eventId.Trim().Replace(' ', '_');
        return $"ctr.bonus.{normalizedEventId}.{monthIndex:0000}.{effectIndex:00}.{sequence:0000}";
    }

    private string BuildHistoricalCampaignStatusLabel()
    {
        if (_historicalCampaign is null)
        {
            return _campaignActionMessage;
        }

        var nextEvent = _historicalCampaign.GetNextUpcomingUnfiredEvent(_currentDate, _historicalCampaignState);
        if (!string.IsNullOrWhiteSpace(_campaignActionMessage))
        {
            if (nextEvent is null)
            {
                return $"{_campaignActionMessage} No further events are scheduled from the current date.";
            }

            return
                $"{_campaignActionMessage} Next: '{nextEvent.Title}' in {FormatSimulationDate(nextEvent.GetDate())}.";
        }

        if (nextEvent is null)
        {
            return "Historical campaign: no further events are scheduled from the current date.";
        }

        return $"Historical campaign: next event '{nextEvent.Title}' in {FormatSimulationDate(nextEvent.GetDate())}.";
    }

    private void DrawHistoricalArchiveTab()
    {
        DrawSection("Historical Archive");
        GUILayout.Label("Open this tab to review historical campaign events that already happened.");
        DrawMetric("Archived Events", _historicalCampaignArchive.Count, "0");
        if (_historicalCampaignArchive.Count > 0
            && GUILayout.Button("Scroll To Latest", GUILayout.Height(26f)))
        {
            _historicalCampaignArchiveScroll = Vector2.zero;
        }
    }

    private void DrawHistoricalArchiveReport()
    {
        DrawSection("Historical Archive");
        DrawHistoricalCampaignArchivePanel(maxHeight: 600f, includeHeader: true);
    }

    private void DrawHistoricalCampaignArchivePanel(float maxHeight, bool includeHeader)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        if (includeHeader)
        {
            GUILayout.Label($"Historical archive ({_historicalCampaignArchive.Count})");
        }

        if (_historicalCampaignArchive.Count == 0)
        {
            GUILayout.Label("No archived events yet.");
            GUILayout.EndVertical();
            return;
        }

        _historicalCampaignArchiveScroll = GUILayout.BeginScrollView(
            _historicalCampaignArchiveScroll,
            GUILayout.Height(maxHeight));

        for (var index = _historicalCampaignArchive.Count - 1; index >= 0; index--)
        {
            var entry = _historicalCampaignArchive[index];
            GUILayout.Label($"{FormatSimulationDate(entry.GetDate())} - {entry.Title}");
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawHistoricalCampaignDebugPanel()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        if (GUILayout.Button(
                _showHistoricalCampaignDebugPanel
                    ? "Hide Historical Debug"
                    : "Show Historical Debug"))
        {
            _showHistoricalCampaignDebugPanel = !_showHistoricalCampaignDebugPanel;
        }

        if (_showHistoricalCampaignDebugPanel)
        {
            GUILayout.Label("Historical campaign debug tools");
            GUILayout.Label($"Current date: {FormatSimulationDate(_currentDate)}");

            if (_historicalCampaign is null)
            {
                GUILayout.Label("Historical campaign service is unavailable.");
            }
            else
            {
                var nextEvent = _historicalCampaign.GetNextUpcomingUnfiredEvent(_currentDate, _historicalCampaignState);
                GUILayout.Label(nextEvent is null
                    ? "Next event: none"
                    : $"Next event: {nextEvent.Title} ({nextEvent.Year:D4}-{nextEvent.Month:D2})");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Year", GUILayout.Width(40f));
                _historicalCampaignDebugYearInput = GUILayout.TextField(_historicalCampaignDebugYearInput, GUILayout.Width(80f));
                GUILayout.Label("Month", GUILayout.Width(50f));
                _historicalCampaignDebugMonthInput = GUILayout.TextField(_historicalCampaignDebugMonthInput, GUILayout.Width(60f));
                if (GUILayout.Button("Set Date (Debug)", GUILayout.Width(150f)))
                {
                    ApplyHistoricalCampaignDebugDateOverride();
                }

                if (GUILayout.Button("Trigger Next Event (Debug)", GUILayout.Width(210f)))
                {
                    TriggerNextHistoricalCampaignEventForDebug();
                }

                GUILayout.EndHorizontal();
                GUILayout.Label("Debug date override only moves simulation date; it does not replay skipped monthly simulation.");
            }

            if (!string.IsNullOrWhiteSpace(_historicalCampaignDebugMessage))
            {
                GUILayout.Label(_historicalCampaignDebugMessage);
            }
        }

        GUILayout.EndVertical();
    }

    private void AppendHistoricalCampaignArchiveEntry(HistoricalCampaignEventDefinition definition)
    {
        _historicalCampaignArchive.Add(HistoricalCampaignArchiveEntry.FromEvent(definition));
        _historicalCampaignArchiveScroll = Vector2.zero;
        Debug.Log($"[Campaign] Archive appended: {definition.Title} | {definition.Year:D4}-{definition.Month:D2}");
    }

    private void RestoreHistoricalCampaignArchive(IReadOnlyList<HistoricalCampaignArchiveEntry>? archiveEntries)
    {
        _historicalCampaignArchive.Clear();
        _historicalCampaignArchiveScroll = Vector2.zero;

        if (archiveEntries is null)
        {
            return;
        }

        for (var index = 0; index < archiveEntries.Count; index++)
        {
            var entry = archiveEntries[index];
            if (entry is null || string.IsNullOrWhiteSpace(entry.Title))
            {
                continue;
            }

            try
            {
                _ = new SimulationDate(entry.Year, entry.Month);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            _historicalCampaignArchive.Add(new HistoricalCampaignArchiveEntry
            {
                Title = entry.Title.Trim(),
                Year = entry.Year,
                Month = entry.Month,
            });
        }
    }

    private void LogHistoricalCampaignContractModifiers()
    {
        if (_historicalCampaignContractModifierSource is null)
        {
            return;
        }

        var modifiers = _historicalCampaignContractModifierSource.GetCurrentModifiers();
        var fuelConsumptionUnlocked = modifiers.IsContractParameterUnlocked(ContractBalance.HistoricalCampaignContractParameterFuelConsumption);
        Debug.Log(
            $"[Campaign] Active contract modifiers: civilMix={modifiers.CivilCategoryWeightMultiplier:F3}, " +
            $"militaryMix={modifiers.MilitaryCategoryWeightMultiplier:F3}, " +
            $"civilPrice={modifiers.CivilPriceMultiplier:F3}, " +
            $"militaryPrice={modifiers.MilitaryPriceMultiplier:F3}, " +
            $"fuelParamUnlocked={fuelConsumptionUnlocked}");
    }

    private void LogHistoricalCampaignTechnologyModifiersIfYearChanged()
    {
        if (_historicalCampaignTechnologyModifierSource is null || _currentDate.Year == _lastLoggedTechnologyModifierYear)
        {
            return;
        }

        var modifiers = _historicalCampaignTechnologyModifierSource.GetCurrentModifiers();
        _lastLoggedTechnologyModifierYear = _currentDate.Year;
        Debug.Log(
            $"[Campaign] Active tech modifiers ({_currentDate.Year:D4}): " +
            $"engine={modifiers.EnginePerformanceMultiplier:F3}, " +
            $"airframe={modifiers.AirframeEfficiencyMultiplier:F3}, " +
            $"aero={modifiers.AerodynamicsEfficiencyMultiplier:F3}, " +
            $"fuel={modifiers.FuelEfficiencyMultiplier:F3}");
    }

    private void SyncHistoricalCampaignDebugInputsToCurrentDate()
    {
        _historicalCampaignDebugYearInput = _currentDate.Year.ToString();
        _historicalCampaignDebugMonthInput = _currentDate.Month.ToString("00");
    }

    private void ApplyHistoricalCampaignDebugDateOverride()
    {
        if (!int.TryParse(_historicalCampaignDebugYearInput?.Trim(), out var year) ||
            !int.TryParse(_historicalCampaignDebugMonthInput?.Trim(), out var month))
        {
            _historicalCampaignDebugMessage = "Debug date override failed: year/month must be numeric.";
            return;
        }

        try
        {
            var overriddenDate = new SimulationDate(year, month);
            _currentDate = overriddenDate;
            _lastLoggedTechnologyModifierYear = int.MinValue;
            LogHistoricalCampaignTechnologyModifiersIfYearChanged();
            RebuildEnginePreview();
            _historicalCampaignDebugMessage = $"Debug date set to {FormatSimulationDate(_currentDate)}.";
            Debug.Log($"[Campaign][Debug] Date override applied: {_currentDate}");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _historicalCampaignDebugMessage = $"Debug date override failed: {ex.Message}";
        }
    }

    private void TriggerNextHistoricalCampaignEventForDebug()
    {
        if (_historicalCampaign is null)
        {
            _historicalCampaignDebugMessage = "Cannot trigger event: historical campaign service is unavailable.";
            return;
        }

        var nextEvent = _historicalCampaign.GetNextUnfiredEvent(_historicalCampaignState);
        if (nextEvent is null)
        {
            _historicalCampaignDebugMessage = "No unfired historical campaign events remain.";
            return;
        }

        _currentDate = nextEvent.GetDate();
        SyncHistoricalCampaignDebugInputsToCurrentDate();
        _lastLoggedTechnologyModifierYear = int.MinValue;
        LogHistoricalCampaignTechnologyModifiersIfYearChanged();
        RebuildEnginePreview();
        var bonusBatchRequests = ProcessHistoricalCampaignForCurrentDate();
        var injectedContracts = InjectHistoricalCampaignBonusContractsForCurrentMonth(bonusBatchRequests);
        if (injectedContracts.Count > 0)
        {
            _lastGeneratedContracts.AddRange(injectedContracts);
        }

        _historicalCampaignDebugMessage = injectedContracts.Count > 0
            ? $"Triggered '{nextEvent.Title}' at {_currentDate}; injected {injectedContracts.Count} bonus contracts."
            : $"Triggered '{nextEvent.Title}' at {_currentDate}.";
        Debug.Log($"[Campaign][Debug] {_historicalCampaignDebugMessage}");
    }

    private static void LogGeneratedContractMix(IReadOnlyList<Contract> generatedContracts)
    {
        if (generatedContracts is null || generatedContracts.Count == 0)
        {
            return;
        }

        var militaryCount = 0;
        var civilCount = 0;
        for (var index = 0; index < generatedContracts.Count; index++)
        {
            var category = generatedContracts[index].Requirements.RequiredCategory;
            if (!category.HasValue)
            {
                continue;
            }

            if (ContractBalance.IsMilitaryContractCategory(category.Value))
            {
                militaryCount++;
            }
            else
            {
                civilCount++;
            }
        }

        Debug.Log($"[Contracts] Monthly mix: civil={civilCount}, military={militaryCount}");
    }

    private IReadOnlyList<AircraftCategory> GetUnlockedAircraftCategoriesForCurrentState()
    {
        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        var unlockedCategories = new List<AircraftCategory>(profiles.Count);
        for (var index = 0; index < profiles.Count; index++)
        {
            var category = profiles[index].Category;
            if (!IsAircraftCategoryUnlockedForCurrentState(category))
            {
                continue;
            }

            unlockedCategories.Add(category);
        }

        return unlockedCategories;
    }

    private bool IsAircraftCategoryUnlockedForCurrentState(AircraftCategory category)
    {
        if (_historicalCampaignContractModifierSource is null)
        {
            return true;
        }

        return _historicalCampaignContractModifierSource.GetCurrentModifiers().IsCategoryUnlocked(category);
    }

    private bool IsCompositeMaterialUnlockedForCurrentState()
    {
        if (_historicalCampaign is null)
        {
            return _currentDate.Year >= PlaneDesignerBalance.Airframe.CompositeMaterialFallbackUnlockYear;
        }

        return _historicalCampaignState.HasFired(PlaneDesignerBalance.Airframe.CompositeMaterialUnlockEventId)
            || _currentDate.Year >= PlaneDesignerBalance.Airframe.CompositeMaterialFallbackUnlockYear;
    }

    private void NormalizeSelectedAircraftCategoryForHistoricalCampaign()
    {
        if (IsAircraftCategoryUnlockedForCurrentState(_selectedAircraftCategory))
        {
            return;
        }

        var unlockedCategories = GetUnlockedAircraftCategoriesForCurrentState();
        if (unlockedCategories.Count == 0)
        {
            return;
        }

        var previousCategory = _selectedAircraftCategory;
        _selectedAircraftCategory = unlockedCategories[0];
        Debug.Log(
            $"[Campaign] Category '{previousCategory}' is currently locked. Switched selection to '{_selectedAircraftCategory}'.");
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

    private static string FormatApprovedYear(int? year) => year.HasValue && year.Value >= 1
        ? FormatUiInteger(year.Value)
        : "Not approved";

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

    private double DrawWingAreaSliderWithOptimizationButton(string label, double value, double min, double max, string format)
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
        var sliderPosition = ToExponentialSliderPosition(currentValue, min, max);
        var nextSliderPosition = GUILayout.HorizontalSlider(sliderPosition, 0f, 1f, GUILayout.ExpandWidth(true));
        GUI.SetNextControlName(controlName);
        var nextInput = GUILayout.TextField(currentInput, GUILayout.Width(120f));
        GUILayout.Label(
            $"[{FormatUiNumber(min, format)} - {FormatUiNumber(max, format)}]",
            GUILayout.Width(185f));
        var optimizeClicked = GUILayout.Button("Best Payload", GUILayout.Width(120f), GUILayout.Height(24f));
        GUILayout.EndHorizontal();

        var hasFocus = GUI.GetNameOfFocusedControl() == controlName;
        var pressedEnter = hasFocus
            && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        var lostFocus = hadFocus && !hasFocus;

        var sliderMappedValue = FromExponentialSliderPosition(nextSliderPosition, min, max);
        var sliderRounded = Math.Clamp(Math.Round(sliderMappedValue, 4, MidpointRounding.AwayFromZero), min, max);
        var sliderChanged = Math.Abs(sliderRounded - currentValue) > 1e-6;

        if (sliderChanged)
        {
            currentValue = sliderRounded;
        }

        _doubleSliderInputBuffer[key] = nextInput;

        if (pressedEnter || lostFocus)
        {
            if (TryParseDecimalCandidate(nextInput, out var parsedValue)
                && parsedValue >= min
                && parsedValue <= max)
            {
                currentValue = Math.Round(parsedValue, 4, MidpointRounding.AwayFromZero);
            }

            if (pressedEnter)
            {
                Event.current.Use();
            }
        }

        if (optimizeClicked)
        {
            FindBestWingAreaForMaximumPayloadCapacity(min, max);
            _doubleSliderInputBuffer[key] = FormatUiNumber(_typeWingAreaSquareMeters, format);
            _doubleSliderHadFocus[key] = false;
            return _typeWingAreaSquareMeters;
        }

        if (!hasFocus || sliderChanged)
        {
            _doubleSliderInputBuffer[key] = FormatUiNumber(currentValue, format);
        }

        _doubleSliderHadFocus[key] = hasFocus;
        return currentValue;
    }

    private double DrawSlider(
        string label,
        double value,
        double min,
        double max,
        string format,
        ModifierTooltipDefinition? tooltip = null,
        bool useLinearScale = false)
    {
        var currentValue = Math.Clamp(value, min, max);
        var keyFamily = useLinearScale ? "double.linear" : "double";
        var key = BuildNumericFieldKey(keyFamily, label, min, max);
        var controlName = BuildControlName("double", key);
        _doubleSliderHadFocus.TryGetValue(key, out var hadFocus);

        if (!_doubleSliderInputBuffer.TryGetValue(key, out var currentInput))
        {
            currentInput = FormatUiNumber(currentValue, format);
            _doubleSliderInputBuffer[key] = currentInput;
        }

        var labelText = $"{label}: {FormatUiNumber(currentValue, format)}";
        if (tooltip.HasValue)
        {
            DrawModifierLabelWithTooltip(labelText, tooltip.Value);
        }
        else
        {
            GUILayout.Label(labelText);
        }

        GUILayout.BeginHorizontal();
        var sliderPosition = useLinearScale
            ? ToLinearSliderPosition(currentValue, min, max)
            : ToExponentialSliderPosition(currentValue, min, max);
        var nextSliderPosition = GUILayout.HorizontalSlider(sliderPosition, 0f, 1f, GUILayout.ExpandWidth(true));
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

        var sliderMappedValue = useLinearScale
            ? FromLinearSliderPosition(nextSliderPosition, min, max)
            : FromExponentialSliderPosition(nextSliderPosition, min, max);
        var sliderRounded = Math.Clamp(Math.Round(sliderMappedValue, 4, MidpointRounding.AwayFromZero), min, max);
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

    private int DrawIntSlider(string label, int value, int min, int max, bool useLinearScale = false)
    {
        var currentValue = Math.Clamp(value, min, max);
        var keyFamily = useLinearScale ? "int.linear" : "int";
        var key = BuildNumericFieldKey(keyFamily, label, min, max);
        var controlName = BuildControlName("int", key);
        _intSliderHadFocus.TryGetValue(key, out var hadFocus);

        if (!_intSliderInputBuffer.TryGetValue(key, out var currentInput))
        {
            currentInput = FormatUiInteger(currentValue);
            _intSliderInputBuffer[key] = currentInput;
        }

        GUILayout.Label($"{label}: {FormatUiInteger(currentValue)}");
        GUILayout.BeginHorizontal();
        var sliderPosition = useLinearScale
            ? ToLinearSliderPosition(currentValue, min, max)
            : ToExponentialSliderPosition(currentValue, min, max);
        var nextSliderPosition = GUILayout.HorizontalSlider(sliderPosition, 0f, 1f, GUILayout.ExpandWidth(true));
        GUI.SetNextControlName(controlName);
        var nextInput = GUILayout.TextField(currentInput, GUILayout.Width(120f));
        GUILayout.Label($"[{FormatUiInteger(min)} - {FormatUiInteger(max)}]", GUILayout.Width(185f));
        GUILayout.EndHorizontal();

        var hasFocus = GUI.GetNameOfFocusedControl() == controlName;
        var pressedEnter = hasFocus
            && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        var lostFocus = hadFocus && !hasFocus;

        var sliderMappedValue = useLinearScale
            ? FromLinearSliderPosition(nextSliderPosition, min, max)
            : FromExponentialSliderPosition(nextSliderPosition, min, max);
        var sliderRounded = Mathf.Clamp(Mathf.RoundToInt((float)sliderMappedValue), min, max);
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

    private void FindBestWingAreaForMaximumPayloadCapacity(double minWingArea, double maxWingArea)
    {
        var originalWingArea = Math.Clamp(_typeWingAreaSquareMeters, minWingArea, maxWingArea);
        var bestWingArea = originalWingArea;
        var bestPayloadKilograms = 0d;
        var hasBestCandidate = false;
        var lastError = string.Empty;

        ScanWingAreaRangeForPayload(
            minWingArea,
            maxWingArea,
            step: 5d,
            originalWingArea,
            ref hasBestCandidate,
            ref bestWingArea,
            ref bestPayloadKilograms,
            ref lastError);

        if (hasBestCandidate)
        {
            ScanWingAreaRangeForPayload(
                Math.Max(minWingArea, bestWingArea - 10d),
                Math.Min(maxWingArea, bestWingArea + 10d),
                step: 1d,
                originalWingArea,
                ref hasBestCandidate,
                ref bestWingArea,
                ref bestPayloadKilograms,
                ref lastError);

            ScanWingAreaRangeForPayload(
                Math.Max(minWingArea, bestWingArea - 1d),
                Math.Min(maxWingArea, bestWingArea + 1d),
                step: 0.1d,
                originalWingArea,
                ref hasBestCandidate,
                ref bestWingArea,
                ref bestPayloadKilograms,
                ref lastError);
        }

        if (!hasBestCandidate)
        {
            _typeWingAreaSquareMeters = originalWingArea;
            RebuildAircraftPreview();
            _factoryActionMessage = string.IsNullOrWhiteSpace(lastError)
                ? "Wing area optimization failed: aircraft preview is unavailable for current setup."
                : $"Wing area optimization failed: {lastError}";
            return;
        }

        _typeWingAreaSquareMeters = Math.Round(bestWingArea, 4, MidpointRounding.AwayFromZero);
        RebuildAircraftPreview();
        _factoryActionMessage =
            $"Best wing area for maximum useful load: {FormatUiNumber(_typeWingAreaSquareMeters, "0.0")} m2 " +
            $"(payload {FormatUiNumber(bestPayloadKilograms, "0.0")} kg).";
    }

    private void ScanWingAreaRangeForPayload(
        double minWingArea,
        double maxWingArea,
        double step,
        double originalWingArea,
        ref bool hasBestCandidate,
        ref double bestWingArea,
        ref double bestPayloadKilograms,
        ref string lastError)
    {
        if (step <= 0d || minWingArea > maxWingArea)
        {
            return;
        }

        var candidateWingArea = minWingArea;
        var guard = 0;
        while (candidateWingArea <= maxWingArea + 1e-9d && guard < 20000)
        {
            var normalizedWingArea = Math.Clamp(
                Math.Round(candidateWingArea, 4, MidpointRounding.AwayFromZero),
                minWingArea,
                maxWingArea);

            if (TryBuildAircraftSnapshotForWingArea(normalizedWingArea, out var candidateAircraft, out var errorMessage))
            {
                var payloadCapacityKilograms = candidateAircraft.Performance.PayloadCapacity.Kilograms;
                if (!double.IsNaN(payloadCapacityKilograms) && !double.IsInfinity(payloadCapacityKilograms))
                {
                    var isBetterPayload = !hasBestCandidate || payloadCapacityKilograms > bestPayloadKilograms + 1e-6d;
                    var isEquivalentPayload = hasBestCandidate
                        && System.Math.Abs(payloadCapacityKilograms - bestPayloadKilograms) <= 1e-6d;
                    var isCloserToOriginal = isEquivalentPayload
                        && System.Math.Abs(normalizedWingArea - originalWingArea) < System.Math.Abs(bestWingArea - originalWingArea);

                    if (isBetterPayload || isCloserToOriginal)
                    {
                        hasBestCandidate = true;
                        bestWingArea = normalizedWingArea;
                        bestPayloadKilograms = payloadCapacityKilograms;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                lastError = errorMessage;
            }

            candidateWingArea += step;
            guard++;
        }
    }

    private bool TryBuildAircraftSnapshotForWingArea(double wingAreaSquareMeters, out Aircraft aircraft, out string errorMessage)
    {
        aircraft = null!;
        errorMessage = string.Empty;

        if (_savedEngines.Count == 0)
        {
            errorMessage = "Save at least one engine first.";
            return false;
        }

        if (_aircraftFactory is null)
        {
            errorMessage = "Aircraft factory is not initialized.";
            return false;
        }

        try
        {
            SavedEngineDesign savedEngine;
            if (_typeVersionDevelopment is not null)
            {
                var sourceEngineIndex = FindSavedEngineIndexByResearchId(_typeVersionDevelopment.SourceEngineResearchId);
                if (sourceEngineIndex < 0 || sourceEngineIndex >= _savedEngines.Count)
                {
                    errorMessage = "Source engine for this type version is missing.";
                    return false;
                }

                savedEngine = _savedEngines[sourceEngineIndex];
            }
            else if (!TryGetSelectedCompatibleSavedEngine(out savedEngine, out errorMessage))
            {
                return false;
            }

            var previousWingArea = _typeWingAreaSquareMeters;
            _typeWingAreaSquareMeters = wingAreaSquareMeters;
            try
            {
                var family = _typeVersionDevelopment is not null
                    ? _typeVersionDevelopment.SourceFamilyDefinition
                    : BuildFamily(savedEngine);
                var type = BuildType(family.Id);
                aircraft = _aircraftFactory.CreateAircraft(family, type);
                return true;
            }
            finally
            {
                _typeWingAreaSquareMeters = previousWingArea;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static float ToExponentialSliderPosition(double value, double min, double max)
    {
        var range = max - min;
        if (range <= 1e-9d)
        {
            return 0f;
        }

        var normalized = Math.Clamp((value - min) / range, 0d, 1d);
        var curved = Math.Pow(normalized, 1d / ExponentialSliderPower);
        return (float)Math.Clamp(curved, 0d, 1d);
    }

    private static double FromExponentialSliderPosition(float sliderPosition, double min, double max)
    {
        var range = max - min;
        if (range <= 1e-9d)
        {
            return min;
        }

        var normalized = Math.Clamp((double)sliderPosition, 0d, 1d);
        var curved = Math.Pow(normalized, ExponentialSliderPower);
        return min + (range * curved);
    }

    private static float ToLinearSliderPosition(double value, double min, double max)
    {
        var range = max - min;
        if (range <= 1e-9d)
        {
            return 0f;
        }

        var normalized = Math.Clamp((value - min) / range, 0d, 1d);
        return (float)normalized;
    }

    private static double FromLinearSliderPosition(float sliderPosition, double min, double max)
    {
        var range = max - min;
        if (range <= 1e-9d)
        {
            return min;
        }

        var normalized = Math.Clamp((double)sliderPosition, 0d, 1d);
        return min + (range * normalized);
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

    private void BeginModifierTooltipFrame()
    {
        _modifierTooltipEntries.Clear();
        _hasModifierTooltipHoverCandidate = false;
        _modifierTooltipHoverCandidateId = string.Empty;
        _modifierTooltipHoverCandidateDistance = float.MaxValue;
    }

    private void ClearModifierTooltipInteraction()
    {
        _modifierTooltipEntries.Clear();
        _hasModifierTooltipHoverCandidate = false;
        _modifierTooltipHoverCandidateId = string.Empty;
        _modifierTooltipHoverCandidateDistance = float.MaxValue;
        _modifierTooltipTrackedId = string.Empty;
        _modifierTooltipTrackedSinceTime = 0f;
        _modifierTooltipVisibleId = string.Empty;
    }

    private void DrawModifierLabelWithTooltip(string label, ModifierTooltipDefinition tooltip)
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        GUILayout.Label(label, GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        DrawModifierTooltipIcon(tooltip);
        GUILayout.EndHorizontal();
    }

    private bool DrawModifierToggleWithTooltip(bool currentValue, string label, ModifierTooltipDefinition tooltip)
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        var nextValue = GUILayout.Toggle(currentValue, label, GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        DrawModifierTooltipIcon(tooltip);
        GUILayout.EndHorizontal();
        return nextValue;
    }

    private void DrawModifierTooltipIcon(ModifierTooltipDefinition tooltip)
    {
        var iconRect = GUILayoutUtility.GetRect(
            ModifierTooltipIconSize,
            ModifierTooltipIconSize,
            GUILayout.Width(ModifierTooltipIconSize),
            GUILayout.Height(ModifierTooltipIconSize),
            GUILayout.ExpandWidth(false));

        if (Event.current is not null && Event.current.type == EventType.Repaint)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0.2f, 0.25f, 0.33f, 0.95f);
            GUI.DrawTexture(iconRect, GetModifierTooltipCircleTexture(), ScaleMode.StretchToFill, true);
            GUI.color = previousColor;
        }

        GUI.Label(iconRect, "?", GetModifierTooltipQuestionStyle());
        var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(iconRect.xMax + 8f, iconRect.yMin - 4f));
        _modifierTooltipEntries[tooltip.Id] = new ModifierTooltipFrameEntry(tooltip, anchorScreen);

        var currentEvent = Event.current;
        if (currentEvent is null || currentEvent.type != EventType.Repaint)
        {
            return;
        }

        if (!TryGetModifierTooltipHoverDistance(iconRect, out var distance))
        {
            return;
        }

        if (!_hasModifierTooltipHoverCandidate || distance < _modifierTooltipHoverCandidateDistance)
        {
            _hasModifierTooltipHoverCandidate = true;
            _modifierTooltipHoverCandidateId = tooltip.Id;
            _modifierTooltipHoverCandidateDistance = distance;
        }
    }

    private static bool TryGetModifierTooltipHoverDistance(Rect iconRect, out float distance)
    {
        distance = float.MaxValue;
        if (Event.current is null)
        {
            return false;
        }

        var mousePosition = Event.current.mousePosition;
        distance = Vector2.Distance(mousePosition, iconRect.center);
        return distance <= ModifierTooltipHoverRadiusPixels;
    }

    private void DrawModifierTooltipOverlay(Vector2 virtualScreenSize)
    {
        var currentEvent = Event.current;
        if (currentEvent is null || currentEvent.type != EventType.Repaint)
        {
            return;
        }

        if (_hasModifierTooltipHoverCandidate)
        {
            var now = Time.unscaledTime;
            if (!string.Equals(_modifierTooltipTrackedId, _modifierTooltipHoverCandidateId, StringComparison.Ordinal))
            {
                _modifierTooltipTrackedId = _modifierTooltipHoverCandidateId;
                _modifierTooltipTrackedSinceTime = now;
                _modifierTooltipVisibleId = string.Empty;
            }

            if (now - _modifierTooltipTrackedSinceTime >= ModifierTooltipHoverDelaySeconds)
            {
                _modifierTooltipVisibleId = _modifierTooltipHoverCandidateId;
            }
        }
        else
        {
            _modifierTooltipTrackedId = string.Empty;
            _modifierTooltipTrackedSinceTime = 0f;
            _modifierTooltipVisibleId = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_modifierTooltipVisibleId)
            || !_modifierTooltipEntries.TryGetValue(_modifierTooltipVisibleId, out var entry))
        {
            return;
        }

        DrawModifierTooltipPopup(entry, virtualScreenSize);
    }

    private void DrawModifierTooltipPopup(ModifierTooltipFrameEntry entry, Vector2 virtualScreenSize)
    {
        var titleStyle = GetModifierTooltipTitleStyle();
        var bodyStyle = GetModifierTooltipBodyStyle();
        var bodyText = BuildModifierTooltipBodyText(entry.Definition);

        const float boxWidth = 360f;
        const float padding = 10f;
        const float spacing = 6f;
        const float margin = 8f;
        var contentWidth = boxWidth - (padding * 2f);

        var titleHeight = titleStyle.CalcHeight(new GUIContent(entry.Definition.Title), contentWidth);
        var bodyHeight = bodyStyle.CalcHeight(new GUIContent(bodyText), contentWidth);
        var boxHeight = (padding * 2f) + titleHeight + spacing + bodyHeight;

        var anchorGuiPoint = GUIUtility.ScreenToGUIPoint(entry.AnchorScreen);
        var x = anchorGuiPoint.x;
        var y = anchorGuiPoint.y;

        if (x + boxWidth > virtualScreenSize.x - margin)
        {
            x = Mathf.Max(margin, x - boxWidth - 12f);
        }

        if (y + boxHeight > virtualScreenSize.y - margin)
        {
            y = Mathf.Max(margin, virtualScreenSize.y - boxHeight - margin);
        }

        var outerRect = new Rect(x, y, boxWidth, boxHeight);
        var innerRect = new Rect(outerRect.x + 1f, outerRect.y + 1f, outerRect.width - 2f, outerRect.height - 2f);
        DrawRect(outerRect, new Color(0.09f, 0.11f, 0.14f, 0.98f));
        DrawRect(innerRect, new Color(0.13f, 0.16f, 0.2f, 0.98f));

        var contentX = innerRect.x + padding;
        var contentY = innerRect.y + padding;
        GUI.Label(new Rect(contentX, contentY, contentWidth, titleHeight), entry.Definition.Title, titleStyle);
        contentY += titleHeight + spacing;
        GUI.Label(new Rect(contentX, contentY, contentWidth, bodyHeight), bodyText, bodyStyle);
    }

    private static string BuildModifierTooltipBodyText(ModifierTooltipDefinition tooltip)
    {
        var lines = new List<string>(
            tooltip.PositiveLines.Count +
            tooltip.NegativeLines.Count +
            tooltip.NeutralLines.Count);

        for (var index = 0; index < tooltip.PositiveLines.Count; index++)
        {
            var line = tooltip.PositiveLines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines.Add($"<color=#66D26E>+</color> {line}");
        }

        for (var index = 0; index < tooltip.NegativeLines.Count; index++)
        {
            var line = tooltip.NegativeLines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines.Add($"<color=#F06D6D>-</color> {line}");
        }

        for (var index = 0; index < tooltip.NeutralLines.Count; index++)
        {
            var line = tooltip.NeutralLines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines.Add($"<color=#D7DEE6>{line}</color>");
        }

        if (lines.Count == 0)
        {
            lines.Add("<color=#D7DEE6>No notes available.</color>");
        }

        return string.Join("\n", lines);
    }

    private Texture2D GetModifierTooltipCircleTexture()
    {
        if (_modifierTooltipCircleTexture is not null)
        {
            return _modifierTooltipCircleTexture;
        }

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color[size * size];
        var radius = (size - 1) * 0.5f;
        var center = new Vector2(radius, radius);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixelCenter = new Vector2(x, y);
                var distance = Vector2.Distance(pixelCenter, center);
                var alpha = Mathf.Clamp01(radius - distance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        _modifierTooltipCircleTexture = texture;
        return _modifierTooltipCircleTexture;
    }

    private GUIStyle GetModifierTooltipQuestionStyle()
    {
        if (_modifierTooltipQuestionStyle is not null)
        {
            return _modifierTooltipQuestionStyle;
        }

        _modifierTooltipQuestionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Clip,
            fontSize = Mathf.Max(12, GUI.skin.label.fontSize),
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
        };
        _modifierTooltipQuestionStyle.normal.textColor = new Color(0.95f, 0.98f, 1f, 1f);
        _modifierTooltipQuestionStyle.hover.textColor = new Color(1f, 1f, 1f, 1f);
        _modifierTooltipQuestionStyle.active.textColor = new Color(1f, 1f, 1f, 1f);
        return _modifierTooltipQuestionStyle;
    }

    private GUIStyle GetModifierTooltipTitleStyle()
    {
        if (_modifierTooltipTitleStyle is not null)
        {
            return _modifierTooltipTitleStyle;
        }

        _modifierTooltipTitleStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            richText = false,
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = new Color(0.97f, 0.99f, 1f, 1f),
            },
        };
        return _modifierTooltipTitleStyle;
    }

    private GUIStyle GetModifierTooltipBodyStyle()
    {
        if (_modifierTooltipBodyStyle is not null)
        {
            return _modifierTooltipBodyStyle;
        }

        _modifierTooltipBodyStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            richText = true,
            normal =
            {
                textColor = new Color(0.9f, 0.92f, 0.96f, 1f),
            },
        };
        return _modifierTooltipBodyStyle;
    }

    private static ModifierTooltipDefinition BuildAllowedEngineFamilyTooltipDefinition(AircraftAllowedEngineFamily family) =>
        family == AircraftAllowedEngineFamily.Jet
            ? AllowedEngineFamilyJetTooltip
            : AllowedEngineFamilyPropellerTooltip;

    private static ModifierTooltipDefinition BuildBoostTooltipDefinition(BoostType boostType) =>
        boostType switch
        {
            BoostType.Supercharger => BoostSuperchargerTooltip,
            BoostType.Turbo => BoostTurboTooltip,
            _ => BoostNoneTooltip,
        };

    private static ModifierTooltipDefinition BuildInjectionTooltipDefinition(PistonInjectionType injectionType) =>
        injectionType == PistonInjectionType.DirectInjection
            ? InjectionDirectTooltip
            : InjectionCarburetorTooltip;

    private static ModifierTooltipDefinition BuildAfterburnerTooltipDefinition() => AfterburnerTooltip;

    private static ModifierTooltipDefinition BuildStructureTooltipDefinition(StructureType structureType) =>
        structureType == StructureType.Monocoque
            ? StructureMonocoqueTooltip
            : StructureTrussTooltip;

    private static ModifierTooltipDefinition BuildMaterialTooltipDefinition(MaterialType materialType) =>
        materialType switch
        {
            MaterialType.Aluminum => MaterialAluminumTooltip,
            MaterialType.Composite => MaterialCompositeTooltip,
            _ => MaterialWoodFabricTooltip,
        };

    private static ModifierTooltipDefinition BuildWingPositionTooltipDefinition(WingPosition wingPosition) =>
        wingPosition switch
        {
            WingPosition.High => WingPositionHighTooltip,
            WingPosition.Biplane => WingPositionBiplaneTooltip,
            _ => WingPositionLowTooltip,
        };

    private static ModifierTooltipDefinition BuildWingShapeTooltipDefinition(WingShape wingShape) =>
        wingShape switch
        {
            WingShape.Swept => WingShapeSweptTooltip,
            WingShape.Delta => WingShapeDeltaTooltip,
            _ => WingShapeStraightTooltip,
        };

    private static ModifierTooltipDefinition BuildWingProfileTooltipDefinition(WingProfileType wingProfileType) =>
        wingProfileType switch
        {
            WingProfileType.Naca4DigitLight => WingProfileNacaTooltip,
            WingProfileType.SymmetricalAcrobatic => WingProfileSymmetricalTooltip,
            _ => WingProfileLaminarTooltip,
        };

    private static ModifierTooltipDefinition BuildFlapTooltipDefinition(FlapType flapType) =>
        flapType switch
        {
            FlapType.Plain => FlapPlainTooltip,
            FlapType.Fowler => FlapFowlerTooltip,
            _ => FlapNoneTooltip,
        };

    private static ModifierTooltipDefinition BuildLeadingEdgeFlapsTooltipDefinition() => LeadingEdgeFlapsTooltip;

    private static ModifierTooltipDefinition BuildBackupSystemsTooltipDefinition() => BackupSystemsTooltip;

    private static ModifierTooltipDefinition BuildLandingGearTooltipDefinition(LandingGearType landingGearType) =>
        landingGearType switch
        {
            LandingGearType.Fixed => GearFixedTooltip,
            LandingGearType.Retractable => GearRetractableTooltip,
            _ => GearSemiRetractableTooltip,
        };

    private static ModifierTooltipDefinition BuildRunwayTooltipDefinition(RunwayType runwayType) =>
        runwayType switch
        {
            RunwayType.Short => RunwayShortTooltip,
            RunwayType.Long => RunwayLongTooltip,
            _ => RunwayMediumTooltip,
        };

    private TEnum DrawEnum<TEnum>(string label, TEnum current, ModifierTooltipDefinition? tooltip = null)
        where TEnum : struct, Enum
    {
        if (tooltip.HasValue)
        {
            DrawModifierLabelWithTooltip(label, tooltip.Value);
        }
        else
        {
            GUILayout.Label(label);
        }

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

    private void DrawOptionalEnum<TEnum>(string label, ref bool enabled, ref TEnum value)
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

    private static void DrawContractRequestedCategoryFilter(
        string label,
        ref bool enabled,
        ref AircraftCategory category)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        enabled = GUILayout.Toggle(enabled, $"{label} Enabled");
        if (enabled)
        {
            category = DrawAircraftCategorySelector(label, category, Array.Empty<AircraftCategory>());
        }
        else
        {
            GUILayout.Label($"{label}: All");
        }

        GUILayout.EndVertical();
    }

    private static AircraftCategory DrawAircraftCategorySelector(
        string label,
        AircraftCategory currentCategory,
        IReadOnlyList<AircraftCategory> availableCategories)
    {
        GUILayout.Label(label);
        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        var categories = new List<AircraftCategory>(profiles.Count);
        var labels = new List<string>(profiles.Count);
        for (var index = 0; index < profiles.Count; index++)
        {
            var profile = profiles[index];
            if (availableCategories is not null
                && availableCategories.Count > 0
                && !availableCategories.Contains(profile.Category))
            {
                continue;
            }

            categories.Add(profile.Category);
            labels.Add(profile.DisplayName);
        }

        if (categories.Count == 0)
        {
            categories.Add(currentCategory);
            labels.Add(AircraftCategoryCatalog.GetProfile(currentCategory).DisplayName);
        }

        var selectedIndex = 0;
        for (var index = 0; index < categories.Count; index++)
        {
            if (categories[index] == currentCategory)
            {
                selectedIndex = index;
                break;
            }
        }

        var nextIndex = GUILayout.SelectionGrid(selectedIndex, labels.ToArray(), 1);
        nextIndex = Mathf.Clamp(nextIndex, 0, categories.Count - 1);
        return categories[nextIndex];
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

        if (requirements.MinimumManeuverability.HasValue)
        {
            GUILayout.Label($"Maneuverability >= {FormatUiNumber(requirements.MinimumManeuverability.Value, "0.0")}");
        }

        if (requirements.MinimumCrew.HasValue)
        {
            GUILayout.Label($"Crew >= {FormatUiInteger(requirements.MinimumCrew.Value)}");
        }

        if (requirements.MaximumRequiredRunwayLengthMeters.HasValue)
        {
            GUILayout.Label($"Runway <= {FormatUiNumber(requirements.MaximumRequiredRunwayLengthMeters.Value, "0.0")} m");
        }

        if (requirements.MaximumFuelConsumptionKilogramsPerHour.HasValue)
        {
            GUILayout.Label($"Fuel burn <= {FormatUiNumber(requirements.MaximumFuelConsumptionKilogramsPerHour.Value, "0.0")} kg/h");
        }
    }

    private static double CalculateFuelPerOutputMetric(double output, double fuelKilogramsPerHour, bool isJet)
    {
        var fuelDensityKilogramsPerLiter = isJet ? 0.8d : PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        var fuelLitersPerHour = fuelKilogramsPerHour / System.Math.Max(fuelDensityKilogramsPerLiter, 0.01d);
        return fuelLitersPerHour / System.Math.Max(output, 0.001d);
    }

    private MaterialType DrawMaterialSelector(string label, MaterialType currentMaterial, bool compositeUnlocked)
    {
        DrawModifierLabelWithTooltip(label, BuildMaterialTooltipDefinition(currentMaterial));
        var values = compositeUnlocked
            ? new[] { MaterialType.WoodFabric, MaterialType.Aluminum, MaterialType.Composite }
            : new[] { MaterialType.WoodFabric, MaterialType.Aluminum };
        var labels = compositeUnlocked
            ? new[] { "Wood", "Aluminum", "Composite" }
            : new[] { "Wood", "Aluminum" };
        if (!compositeUnlocked && currentMaterial == MaterialType.Composite)
        {
            currentMaterial = MaterialType.Aluminum;
        }

        var index = Array.IndexOf(values, currentMaterial);
        if (index < 0)
        {
            index = 0;
        }

        var next = GUILayout.SelectionGrid(index, labels, 3);
        next = Mathf.Clamp(next, 0, values.Length - 1);
        return values[next];
    }

    private WingPosition DrawWingPositionSelector(string label, WingPosition wingPosition)
    {
        DrawModifierLabelWithTooltip(label, BuildWingPositionTooltipDefinition(wingPosition));
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

    private WingProfileType DrawWingProfileSelector(string label, WingProfileType wingProfile)
    {
        DrawModifierLabelWithTooltip(label, BuildWingProfileTooltipDefinition(wingProfile));
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

    private FlapType DrawTrailingFlapSelector(string label, FlapType flapType)
    {
        DrawModifierLabelWithTooltip(label, BuildFlapTooltipDefinition(flapType));
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

    private LandingGearType DrawLandingGearSelector(string label, LandingGearType gearType)
    {
        DrawModifierLabelWithTooltip(label, BuildLandingGearTooltipDefinition(gearType));
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

    private RunwayType DrawRunwayTypeSelector(string label, RunwayType runwayType)
    {
        DrawModifierLabelWithTooltip(label, BuildRunwayTooltipDefinition(runwayType));
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

    private EngineDesignerFamily DrawEngineFamilySelector(string label, EngineDesignerFamily currentFamily)
    {
        GUILayout.Label(label);
        var labels = new[] { "Piston", "Jet" };
        var index = currentFamily == EngineDesignerFamily.Jet ? 1 : 0;
        var nextIndex = GUILayout.SelectionGrid(index, labels, 2);
        return nextIndex == 1 ? EngineDesignerFamily.Jet : EngineDesignerFamily.Piston;
    }

    private AircraftAllowedEngineFamily DrawFamilyAllowedEngineSelector(string label, AircraftAllowedEngineFamily currentFamily)
    {
        DrawModifierLabelWithTooltip(label, BuildAllowedEngineFamilyTooltipDefinition(currentFamily));
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

    private EngineType DrawJetTurbineTypeSelector(string label, EngineType currentType)
    {
        GUILayout.Label(label);
        var values = GetAvailableJetTurbineTypesForCurrentDate();
        if (values.Length == 0)
        {
            GUILayout.Label("No jet turbine definitions are available in the current year.");
            return currentType;
        }

        var labels = new string[values.Length];
        for (var optionIndex = 0; optionIndex < values.Length; optionIndex++)
        {
            labels[optionIndex] = values[optionIndex] == EngineType.Turbofan ? "Turbofan" : "Turbojet";
        }

        var index = Array.IndexOf(values, currentType);
        if (index < 0)
        {
            index = 0;
        }

        var nextIndex = GUILayout.SelectionGrid(index, labels, Mathf.Min(2, values.Length));
        nextIndex = Mathf.Clamp(nextIndex, 0, values.Length - 1);
        return values[nextIndex];
    }

    private EngineType[] GetAvailableJetTurbineTypesForCurrentDate()
    {
        var turbojetAvailable = IsEngineTypeAvailableForCurrentDate(EngineType.Turbojet);
        var turbofanAvailable = IsEngineTypeAvailableForCurrentDate(EngineType.Turbofan);
        if (turbojetAvailable && turbofanAvailable)
        {
            return new[] { EngineType.Turbojet, EngineType.Turbofan };
        }

        if (turbojetAvailable)
        {
            return new[] { EngineType.Turbojet };
        }

        if (turbofanAvailable)
        {
            return new[] { EngineType.Turbofan };
        }

        return Array.Empty<EngineType>();
    }

    private BoostType DrawPistonBoostSelector(string label, BoostType currentBoost)
    {
        DrawModifierLabelWithTooltip(label, BuildBoostTooltipDefinition(currentBoost));
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

    private PistonInjectionType DrawPistonInjectionSelector(string label, PistonInjectionType currentInjectionType)
    {
        DrawModifierLabelWithTooltip(label, BuildInjectionTooltipDefinition(currentInjectionType));
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
            var definition = _engineDefinitions[index];
            if (definition.EngineType == requestedType && IsEngineDefinitionAvailableForCurrentDate(definition))
            {
                candidates.Add(index);
            }
        }

        if (candidates.Count == 0)
        {
            for (var index = 0; index < _engineDefinitions.Count; index++)
            {
                var definition = _engineDefinitions[index];
                if (!IsEngineDefinitionAvailableForCurrentDate(definition))
                {
                    continue;
                }

                var engineType = definition.EngineType;
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

    private void NormalizeEngineSelectionForCurrentDate()
    {
        var requestedType = _engineDesignerFamily == EngineDesignerFamily.Piston
            ? _selectedPistonArrangement
            : _selectedJetTurbineType;
        if (!TryResolveEngineDefinitionForType(requestedType, out var selectedDefinitionIndex))
        {
            return;
        }

        _selectedEngineDefinitionIndex = selectedDefinitionIndex;
        _engineDefinitionId = _engineDefinitions[_selectedEngineDefinitionIndex].Id;
        SyncEngineSelectorsFromDefinition(_engineDefinitions[_selectedEngineDefinitionIndex].EngineType);
    }

    private bool IsEngineTypeAvailableForCurrentDate(EngineType engineType)
    {
        for (var index = 0; index < _engineDefinitions.Count; index++)
        {
            var definition = _engineDefinitions[index];
            if (definition.EngineType == engineType && IsEngineDefinitionAvailableForCurrentDate(definition))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEngineDefinitionAvailableForCurrentDate(EngineDefinition definition) =>
        definition.AvailableFromYear <= _currentDate.Year;

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
        var isSportPistonCore = string.Equals(definition.Id, "eng.piston.sport", StringComparison.OrdinalIgnoreCase);
        return new EngineParameters
        {
            DisplacementLiters = isJet
                ? 6d
                : isSportPistonCore
                    ? 2.4d
                    : 17d,
            CylinderCount = isJet ? 12 : isSportPistonCore ? 4 : 12,
            CompressionRatio = isJet ? 16d : isSportPistonCore ? 7.2d : 8.6d,
            BoostType = BoostType.None,
            PistonInjectionType = isSportPistonCore ? PistonInjectionType.Carburetor : PistonInjectionType.DirectInjection,
            OptimizationAltitudeMeters = isJet ? 3600d : isSportPistonCore ? 1200d : 1800d,
            TurbineDiameterMeters = isJet ? 1.35d : 1d,
            BypassRatio = definition.EngineType == EngineType.Turbofan ? 4.2d : 0d,
            MaximumSpeed = isJet ? 45000d : isSportPistonCore ? 1800d : 2500d,
            CoolingEfficiency = isSportPistonCore ? 0.82d : 0.77d,
            EfficiencyOptimizationsPercent = EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent,
            HasAfterburner = false,
        };
    }

    private static AirframeParameters CreateDefaultAirframeParameters(int engineCount)
    {
        return new AirframeParameters
        {
            EngineCount = engineCount,
            StructureType = StructureType.Truss,
            MaterialType = MaterialType.WoodFabric,
            WingPosition = WingPosition.Biplane,
            WingShape = WingShape.Straight,
            WingProfile = WingProfileType.Standard,
            WingAreaSquareMeters = 9.8d,
            TailType = TailType.Classic,
            FlapType = FlapType.Plain,
            HasLeadingEdgeFlaps = false,
            HasBackupSystems = false,
            LandingGearType = LandingGearType.Retractable,
            RunwayType = RunwayType.Short,
            EngineCowling = 0.9d,
            AerodynamicCleanlinessPercent = 20d,
            FuelAmountLiters = 100d,
            CrewCount = 1,
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

    private static int? NormalizeOptionalBaselineYear(int? value)
    {
        if (!value.HasValue || value.Value < 1)
        {
            return null;
        }

        return value.Value;
    }

    private static int NormalizeRequiredYear(int value, int fallbackYear)
    {
        if (value >= 1)
        {
            return value;
        }

        return fallbackYear >= 1
            ? fallbackYear
            : SimulationDate.WrightTycoonStart.Year;
    }

    private static int? NormalizeOptionalMonth(int? value)
    {
        if (!value.HasValue || value.Value < 1 || value.Value > 12)
        {
            return null;
        }

        return value.Value;
    }

    private static bool TryCreateSimulationDate(int? year, int? month, out SimulationDate date)
    {
        date = default;
        var normalizedYear = NormalizeOptionalBaselineYear(year);
        var normalizedMonth = NormalizeOptionalMonth(month);
        if (!normalizedYear.HasValue || !normalizedMonth.HasValue)
        {
            return false;
        }

        date = new SimulationDate(normalizedYear.Value, normalizedMonth.Value);
        return true;
    }

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

    private readonly struct ContractDeliveryCandidate
    {
        public ContractDeliveryCandidate(
            string designId,
            FactoryAircraftTypeCatalogItem catalogItem,
            int stockUnits,
            double unitFulfillmentCost)
        {
            DesignId = designId ?? string.Empty;
            CatalogItem = catalogItem;
            StockUnits = stockUnits;
            UnitFulfillmentCost = unitFulfillmentCost;
        }

        public string DesignId { get; }

        public FactoryAircraftTypeCatalogItem CatalogItem { get; }

        public int StockUnits { get; }

        public double UnitFulfillmentCost { get; }
    }

    private readonly struct ContractDeliveryAllocation
    {
        public ContractDeliveryAllocation(ContractDeliveryCandidate candidate, int unitsToDeliver)
        {
            Candidate = candidate;
            UnitsToDeliver = unitsToDeliver;
        }

        public ContractDeliveryCandidate Candidate { get; }

        public int UnitsToDeliver { get; }
    }

    private readonly struct ModifierTooltipDefinition
    {
        public ModifierTooltipDefinition(
            string id,
            string title,
            IReadOnlyList<string>? positiveLines = null,
            IReadOnlyList<string>? negativeLines = null,
            IReadOnlyList<string>? neutralLines = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "modifier.unknown" : id.Trim();
            Title = string.IsNullOrWhiteSpace(title) ? "Modifier" : title.Trim();
            PositiveLines = positiveLines ?? Array.Empty<string>();
            NegativeLines = negativeLines ?? Array.Empty<string>();
            NeutralLines = neutralLines ?? Array.Empty<string>();
        }

        public string Id { get; }

        public string Title { get; }

        public IReadOnlyList<string> PositiveLines { get; }

        public IReadOnlyList<string> NegativeLines { get; }

        public IReadOnlyList<string> NeutralLines { get; }
    }

    private readonly struct ModifierTooltipFrameEntry
    {
        public ModifierTooltipFrameEntry(ModifierTooltipDefinition definition, Vector2 anchorScreen)
        {
            Definition = definition;
            AnchorScreen = anchorScreen;
        }

        public ModifierTooltipDefinition Definition { get; }

        public Vector2 AnchorScreen { get; }
    }

    private sealed class EngineVersionDevelopmentContext
    {
        public EngineVersionDevelopmentContext(
            string sourceResearchId,
            string sourceDisplayName,
            EngineType lockedEngineType,
            int lockedCylinderCount,
            EngineParameters sourceParameters,
            int sourceDevelopmentStartedYear,
            int variantDevelopmentStartedYear)
        {
            SourceResearchId = sourceResearchId ?? string.Empty;
            SourceDisplayName = sourceDisplayName ?? string.Empty;
            LockedEngineType = lockedEngineType;
            LockedCylinderCount = lockedCylinderCount;
            SourceParameters = sourceParameters?.Clone() ?? new EngineParameters();
            SourceDevelopmentStartedYear = NormalizeRequiredYear(sourceDevelopmentStartedYear, SimulationDate.WrightTycoonStart.Year);
            VariantDevelopmentStartedYear = NormalizeRequiredYear(variantDevelopmentStartedYear, SourceDevelopmentStartedYear);
        }

        public string SourceResearchId { get; }

        public string SourceDisplayName { get; }

        public EngineType LockedEngineType { get; }

        public int LockedCylinderCount { get; }

        public EngineParameters SourceParameters { get; }

        public int SourceDevelopmentStartedYear { get; }

        public int VariantDevelopmentStartedYear { get; }

        public int InitialResearchYear => SourceDevelopmentStartedYear;
    }

    private sealed class TypeVersionDevelopmentContext
    {
        public TypeVersionDevelopmentContext(
            string sourceTypeResearchId,
            string sourceDisplayName,
            string sourceFamilyId,
            string sourceFamilyDisplayName,
            string sourceEngineResearchId,
            AircraftFamilyDefinition sourceFamilyDefinition,
            int sourceDevelopmentStartedYear,
            int variantDevelopmentStartedYear)
        {
            SourceTypeResearchId = sourceTypeResearchId ?? string.Empty;
            SourceDisplayName = sourceDisplayName ?? string.Empty;
            SourceFamilyId = sourceFamilyId ?? string.Empty;
            SourceFamilyDisplayName = sourceFamilyDisplayName ?? string.Empty;
            SourceEngineResearchId = sourceEngineResearchId ?? string.Empty;
            SourceFamilyDefinition = sourceFamilyDefinition ?? throw new ArgumentNullException(nameof(sourceFamilyDefinition));
            SourceDevelopmentStartedYear = NormalizeRequiredYear(sourceDevelopmentStartedYear, SimulationDate.WrightTycoonStart.Year);
            VariantDevelopmentStartedYear = NormalizeRequiredYear(variantDevelopmentStartedYear, SourceDevelopmentStartedYear);
        }

        public string SourceTypeResearchId { get; }

        public string SourceDisplayName { get; }

        public string SourceFamilyId { get; }

        public string SourceFamilyDisplayName { get; }

        public string SourceEngineResearchId { get; }

        public AircraftFamilyDefinition SourceFamilyDefinition { get; }

        public int SourceDevelopmentStartedYear { get; }

        public int VariantDevelopmentStartedYear { get; }

        public int InitialResearchYear => SourceDevelopmentStartedYear;
    }

    private sealed class SavedEngineDesign
    {
        public SavedEngineDesign(
            string name,
            string researchId,
            Engine engine,
            int developmentStartedYear,
            int variantDevelopmentStartedYear,
            int? versionDevelopmentBaselineYear = null,
            string sourceResearchId = "",
            int? approvedYear = null,
            int? frozenYear = null,
            int? frozenMonth = null)
            : this(
                name,
                researchId,
                engine.Definition,
                engine.Parameters,
                developmentStartedYear,
                variantDevelopmentStartedYear,
                versionDevelopmentBaselineYear,
                sourceResearchId,
                approvedYear,
                frozenYear,
                frozenMonth)
        {
        }

        public SavedEngineDesign(
            string name,
            string researchId,
            EngineDefinition definition,
            EngineParameters parameters,
            int developmentStartedYear,
            int variantDevelopmentStartedYear,
            int? versionDevelopmentBaselineYear = null,
            string sourceResearchId = "",
            int? approvedYear = null,
            int? frozenYear = null,
            int? frozenMonth = null)
        {
            Name = name;
            ResearchId = researchId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Parameters = parameters?.Clone() ?? throw new ArgumentNullException(nameof(parameters));
            DevelopmentStartedYear = NormalizeRequiredYear(
                developmentStartedYear,
                NormalizeOptionalBaselineYear(versionDevelopmentBaselineYear) ?? SimulationDate.WrightTycoonStart.Year);
            VariantDevelopmentStartedYear = NormalizeRequiredYear(variantDevelopmentStartedYear, DevelopmentStartedYear);
            VersionDevelopmentBaselineYear = NormalizeOptionalBaselineYear(versionDevelopmentBaselineYear) ?? DevelopmentStartedYear;
            SourceResearchId = sourceResearchId ?? string.Empty;
            ApprovedYear = NormalizeOptionalBaselineYear(approvedYear);
            SetFrozenDate(frozenYear, frozenMonth);
        }

        public string Name { get; }

        public string ResearchId { get; }

        public EngineDefinition Definition { get; }

        public EngineParameters Parameters { get; }

        public int DevelopmentStartedYear { get; }

        public int VariantDevelopmentStartedYear { get; }

        public int? VersionDevelopmentBaselineYear { get; }

        public string SourceResearchId { get; }

        public int? ApprovedYear { get; private set; }

        public int? FrozenYear { get; private set; }

        public int? FrozenMonth { get; private set; }

        public bool HasFrozenDate => FrozenYear.HasValue && FrozenMonth.HasValue;

        public bool IsVersionDevelopment => !string.IsNullOrWhiteSpace(SourceResearchId);

        public void SetApprovedYear(int? approvedYear) => ApprovedYear = NormalizeOptionalBaselineYear(approvedYear);

        public void SetFrozenDate(SimulationDate? date)
        {
            if (!date.HasValue)
            {
                FrozenYear = null;
                FrozenMonth = null;
                return;
            }

            FrozenYear = NormalizeOptionalBaselineYear(date.Value.Year);
            FrozenMonth = NormalizeOptionalMonth(date.Value.Month);
        }

        public void SetFrozenDate(int? year, int? month)
        {
            if (TryCreateSimulationDate(year, month, out var date))
            {
                SetFrozenDate(date);
                return;
            }

            FrozenYear = null;
            FrozenMonth = null;
        }

        public bool TryGetFrozenDate(out SimulationDate date) => TryCreateSimulationDate(FrozenYear, FrozenMonth, out date);
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
            string typeResearchId,
            int developmentStartedYear,
            int variantDevelopmentStartedYear,
            int? typeVersionDevelopmentBaselineYear = null,
            string sourceTypeResearchId = "",
            int? approvedYear = null,
            int? frozenYear = null,
            int? frozenMonth = null)
        {
            Name = name;
            CatalogItem = catalogItem;
            EngineResearchId = engineResearchId;
            FamilyResearchId = familyResearchId;
            TypeResearchId = typeResearchId;
            DevelopmentStartedYear = NormalizeRequiredYear(
                developmentStartedYear,
                NormalizeOptionalBaselineYear(typeVersionDevelopmentBaselineYear) ?? SimulationDate.WrightTycoonStart.Year);
            VariantDevelopmentStartedYear = NormalizeRequiredYear(variantDevelopmentStartedYear, DevelopmentStartedYear);
            TypeVersionDevelopmentBaselineYear = NormalizeOptionalBaselineYear(typeVersionDevelopmentBaselineYear) ?? DevelopmentStartedYear;
            SourceTypeResearchId = sourceTypeResearchId ?? string.Empty;
            ApprovedYear = NormalizeOptionalBaselineYear(approvedYear);
            SetFrozenDate(frozenYear, frozenMonth);
        }

        public string Name { get; }

        public FactoryAircraftTypeCatalogItem CatalogItem { get; }

        public string EngineResearchId { get; }

        public string FamilyResearchId { get; }

        public string TypeResearchId { get; }

        public int DevelopmentStartedYear { get; }

        public int VariantDevelopmentStartedYear { get; }

        public int? TypeVersionDevelopmentBaselineYear { get; }

        public string SourceTypeResearchId { get; }

        public int? ApprovedYear { get; private set; }

        public int? FrozenYear { get; private set; }

        public int? FrozenMonth { get; private set; }

        public bool HasFrozenDate => FrozenYear.HasValue && FrozenMonth.HasValue;

        public bool IsTypeVersionDevelopment => !string.IsNullOrWhiteSpace(SourceTypeResearchId);

        public void SetApprovedYear(int? approvedYear) => ApprovedYear = NormalizeOptionalBaselineYear(approvedYear);

        public void SetFrozenDate(SimulationDate? date)
        {
            if (!date.HasValue)
            {
                FrozenYear = null;
                FrozenMonth = null;
                return;
            }

            FrozenYear = NormalizeOptionalBaselineYear(date.Value.Year);
            FrozenMonth = NormalizeOptionalMonth(date.Value.Month);
        }

        public void SetFrozenDate(int? year, int? month)
        {
            if (TryCreateSimulationDate(year, month, out var date))
            {
                SetFrozenDate(date);
                return;
            }

            FrozenYear = null;
            FrozenMonth = null;
        }

        public bool TryGetFrozenDate(out SimulationDate date) => TryCreateSimulationDate(FrozenYear, FrozenMonth, out date);
    }

    private readonly struct HistoricalCampaignBonusBatchRequest
    {
        public HistoricalCampaignBonusBatchRequest(
            string eventId,
            string eventTitle,
            string balanceKey,
            int effectIndex,
            int targetMonthIndex,
            int requestedContracts,
            double militaryWeightMultiplier)
        {
            EventId = eventId;
            EventTitle = eventTitle;
            BalanceKey = balanceKey;
            EffectIndex = effectIndex;
            TargetMonthIndex = targetMonthIndex;
            RequestedContracts = requestedContracts;
            MilitaryWeightMultiplier = militaryWeightMultiplier;
        }

        public string EventId { get; }

        public string EventTitle { get; }

        public string BalanceKey { get; }

        public int EffectIndex { get; }

        public int TargetMonthIndex { get; }

        public int RequestedContracts { get; }

        public double MilitaryWeightMultiplier { get; }
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

        public int MyMachinesSubTab { get; set; }

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

        public bool ContractRequestedCategoryFilterEnabled { get; set; }

        public AircraftCategory ContractRequestedCategoryFilter { get; set; }

        public int ContractQuantityFilterMin { get; set; }

        public int ContractQuantityFilterMax { get; set; }

        public bool DisablePendingProposalAdvancePrompt { get; set; }

        public NotebookWindowSaveState GovernmentContractNotebookState { get; set; } = new();

        public EngineParameters EngineParameters { get; set; } = new();

        public AirframeParameters BaseAirframeParameters { get; set; } = new();

        public List<SavedEngineDesignState> SavedEngines { get; set; } = new();

        public List<SavedFamilyDesignState> SavedFamilies { get; set; } = new();

        public List<SavedAircraftDesignState> SavedAircraftDesigns { get; set; } = new();

        public List<ResearchModifierStateEntry> ResearchModifiersByResearchId { get; set; } = new();

        public List<StringStringStateEntry> SelectedWarehouseDesignByContractId { get; set; } = new();

        public List<ContractExcludedWarehouseDesignStateEntry> ExcludedWarehouseDesignByContractId { get; set; } = new();

        public List<StringDoubleStateEntry> DeliveredContractProfitById { get; set; } = new();

        public List<string> FiredHistoricalCampaignEventIds { get; set; } = new();

        public List<HistoricalCampaignArchiveEntry> HistoricalCampaignArchiveEntries { get; set; } = new();

        public ContractBoardSnapshot? ContractBoardSnapshot { get; set; }

        public GovernmentContractBoardSnapshot? GovernmentContractBoardSnapshot { get; set; }

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

        public int DevelopmentStartedYear { get; set; }

        public int VariantDevelopmentStartedYear { get; set; }

        public int? ApprovedYear { get; set; }

        public int? FrozenYear { get; set; }

        public int? FrozenMonth { get; set; }

        // Legacy field kept for backward compatibility with older saves.
        public int? VersionDevelopmentBaselineYear { get; set; }

        public string SourceResearchId { get; set; } = string.Empty;
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

        public int DevelopmentStartedYear { get; set; }

        public int VariantDevelopmentStartedYear { get; set; }

        public int? ApprovedYear { get; set; }

        public int? FrozenYear { get; set; }

        public int? FrozenMonth { get; set; }

        // Legacy field kept for backward compatibility with older saves.
        public int? TypeVersionDevelopmentBaselineYear { get; set; }

        public string SourceTypeResearchId { get; set; } = string.Empty;
    }

    private sealed class ResearchModifierStateEntry
    {
        public string ResearchId { get; set; } = string.Empty;

        public int ReliabilityPoints { get; set; }

        public int ProductionOptimizationPoints { get; set; }

        public int FastDevelopmentPoints { get; set; }
    }

    private sealed class ContractExcludedWarehouseDesignStateEntry
    {
        public string ContractId { get; set; } = string.Empty;

        public List<string> DesignIds { get; set; } = new();
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












