using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyperGPU.Models;
using HyperGPU.Services;

namespace HyperGPU.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IHostInspectionService _hostInspectionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IHostQuickActionService _hostQuickActionService;
    private readonly IAppResourceService _resources;
    private readonly IAppStateService _appStateService;
    private readonly VmExecutionPlanBuilder _vmExecutionPlanBuilder;
    private readonly IVmProvisioningService _vmProvisioningService;

    private readonly List<VmExecutionStageResult> _executionStagesBuffer = [];
    private readonly Lock _executionStagesLock = new();
    private VmExecutionPlan? _latestPlan;
    private HostInspectionSnapshot? _latestSnapshot;

    public MainViewModel(IHostInspectionService hostInspectionService, IFilePickerService filePickerService, IHostQuickActionService hostQuickActionService, IAppResourceService resources, IAppStateService appStateService, VmExecutionPlanBuilder vmExecutionPlanBuilder, IVmProvisioningService vmProvisioningService)
    {
        _hostInspectionService = hostInspectionService ?? throw new ArgumentNullException(nameof(hostInspectionService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _hostQuickActionService = hostQuickActionService ?? throw new ArgumentNullException(nameof(hostQuickActionService));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
        _vmExecutionPlanBuilder = vmExecutionPlanBuilder ?? throw new ArgumentNullException(nameof(vmExecutionPlanBuilder));
        _vmProvisioningService = vmProvisioningService ?? throw new ArgumentNullException(nameof(vmProvisioningService));

        Checks = [];
        GpuDevices = [];
        GpuSelectionOptions = ["AUTO"];
        NetworkSwitches = ["Default Switch"];
        ExecutionStages = [];
        VmPlanIssues = [];
        BrowseIsoCommand = new AsyncRelayCommand(BrowseIsoAsync, CanBrowsePaths);
        BrowseVhdPathCommand = new AsyncRelayCommand(BrowseVhdPathAsync, CanBrowsePaths);
        ExecuteCheckQuickActionCommand = new AsyncRelayCommand<HostCheckResult>(ExecuteCheckQuickActionAsync);
        ExecutePrimaryQuickActionCommand = new AsyncRelayCommand(ExecutePrimaryQuickActionAsync, () => HasPrimaryQuickAction);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        BuildVmPlanCommand = new RelayCommand(BuildVmPlan);
        ExecuteVmPlanCommand = new AsyncRelayCommand(ExecuteVmPlanAsync, CanExecuteVmPlan);
        UpdateExistingVmCommand = new AsyncRelayCommand(UpdateExistingVmAsync, CanExecuteVmPlan);
        StatusSummary = _resources.GetString("SummaryNotRun");
        CreateVmParametersPreview = _resources.GetString("VmPlanNotGenerated");
        DriverUpdateCommandPreview = _resources.GetString("VmPlanNotGenerated");
        ExecutionLog = _resources.GetString("VmExecutionNotRun");
        ExecutionStagesText = _resources.GetString("VmExecutionNotRun");
        ExecutionStatus = _resources.GetString("VmExecutionNotRun");
        PostProvisioningGuideText = _resources.GetString("PostProvisioningGuideText");
        VmPlanIssuesText = _resources.GetString("VmPlanNotGenerated");
        VmPlanSummary = _resources.GetString("VmPlanNotGenerated");
        VmName = _appStateService.GetString("VmName", "GPUPV");
        Username = _appStateService.GetString("Username", "GPUVM");
        Password = "CoolestPassword!";
        CpuCoresText = _appStateService.GetString("CpuCoresText", "4");
        MemoryGbText = _appStateService.GetString("MemoryGbText", "8");
        DiskSizeGbText = _appStateService.GetString("DiskSizeGbText", "40");
        GpuSharePercentageText = _appStateService.GetString("GpuSharePercentageText", "50");
        SelectedGpuName = _appStateService.GetString("SelectedGpuName", "AUTO");
        SelectedNetworkSwitch = _appStateService.GetString("SelectedNetworkSwitch", "Default Switch");
        SourcePath = _appStateService.GetString("SourcePath");
        SelectedTheme = _appStateService.GetString("SelectedTheme", "Dark");
        VhdPath = _appStateService.GetString("VhdPath", @"C:\Users\Public\Documents\Hyper-V\Virtual Hard Disks\");
        IsAutoLogonEnabled = _appStateService.GetBoolean("IsAutoLogonEnabled", true);

        GpuDevices.CollectionChanged += OnGpuDevicesCollectionChanged;
        NetworkSwitches.CollectionChanged += OnNetworkSwitchesCollectionChanged;
    }

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private bool _hasSnapshot;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isAutoLogonEnabled = true;

    [ObservableProperty]
    private string _architectureText = string.Empty;

    [ObservableProperty]
    private string _lastUpdatedText = string.Empty;

    [ObservableProperty]
    private bool _hasExecutionPlan;

    [ObservableProperty]
    private string _cpuCoresText = string.Empty;

    [ObservableProperty]
    private string _createVmParametersPreview = string.Empty;

    [ObservableProperty]
    private string _diskSizeGbText = string.Empty;

    [ObservableProperty]
    private string _driverUpdateCommandPreview = string.Empty;

    [ObservableProperty]
    private string _executionLog = string.Empty;

    [ObservableProperty]
    private string _executionStagesText = string.Empty;

    [ObservableProperty]
    private string _executionStatus = string.Empty;

    [ObservableProperty]
    private string _postProvisioningGuideText = string.Empty;

    [ObservableProperty]
    private string _gpuSharePercentageText = string.Empty;

    [ObservableProperty]
    private bool _hasExecutionOutput;

    [ObservableProperty]
    private bool _isExecutionSuccessful;

    [ObservableProperty]
    private bool _isExecutingPlan;

    [ObservableProperty]
    private string _operatingSystemText = string.Empty;

    [ObservableProperty]
    private string _memoryGbText = string.Empty;

    [ObservableProperty]
    private int _readyCount;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _selectedGpuName = "AUTO";

    [ObservableProperty]
    private string _selectedNetworkSwitch = string.Empty;

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _statusSummary = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _vhdPath = string.Empty;

    [ObservableProperty]
    private string _vmName = string.Empty;

    [ObservableProperty]
    private string _vmPlanSummary = string.Empty;

    [ObservableProperty]
    private string _vmPlanIssuesText = string.Empty;

    [ObservableProperty]
    private string _validationSummary = string.Empty;

    [ObservableProperty]
    private int _warningCount;

    public ObservableCollection<HostCheckResult> Checks { get; }

    public ObservableCollection<GpuDeviceInfo> GpuDevices { get; }

    public ObservableCollection<string> GpuSelectionOptions { get; }

    public ObservableCollection<VmExecutionStageResult> ExecutionStages { get; }

    public ObservableCollection<string> NetworkSwitches { get; }

    public ObservableCollection<VmPlanIssue> VmPlanIssues { get; }

    public bool HasGpuDevices => GpuDevices.Count > 0;

    public bool HasNetworkSwitches => NetworkSwitches.Count > 0;

    public bool ShowNetworkSwitchFallback => !HasNetworkSwitches;

    public bool HasValidationIssues => !string.IsNullOrWhiteSpace(ValidationSummary);

    public bool HasVmNameIssue => HasIssue("VM name", "VMName", "VM name");

    public bool HasUsernameIssue => HasIssue("username", "Username", "Guest username");

    public bool HasPasswordIssue => HasIssue("password", "Password", "guest password");

    public bool HasIsoPathIssue => HasIssue("ISO", "ISO path");

    public bool HasVhdPathIssue => HasIssue("VHD folder", "VHD path");

    public bool HasCpuIssue => HasIssue("CPU");

    public bool HasMemoryIssue => HasIssue("Memory");

    public bool HasDiskSizeIssue => HasIssue("Disk size");

    public bool HasGpuShareIssue => HasIssue("GPU allocation");

    public bool HasNetworkSwitchIssue => HasIssue("network switch", "Hyper-V network switch");

    public bool HasGpuIssue => HasIssue("GPU option", "GPU selection");

    public IRelayCommand BuildVmPlanCommand { get; }

    public IAsyncRelayCommand BrowseIsoCommand { get; }

    public IAsyncRelayCommand BrowseVhdPathCommand { get; }

    public IAsyncRelayCommand ExecuteVmPlanCommand { get; }

    public IAsyncRelayCommand UpdateExistingVmCommand { get; }

    public IAsyncRelayCommand<HostCheckResult> ExecuteCheckQuickActionCommand { get; }

    public IAsyncRelayCommand ExecutePrimaryQuickActionCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public bool IsOverviewComplete => HasSnapshot && ErrorCount == 0;

    public bool IsOverviewIncomplete => !IsOverviewComplete;

    public bool CanContinueFromOverview => IsOverviewComplete;

    public bool IsConfigurationComplete => _latestPlan is { HasBlockingIssues: false };

    public bool IsConfigurationIncomplete => !IsConfigurationComplete;

    public bool CanContinueFromConfiguration => IsConfigurationComplete;

    public bool IsExecutionComplete => HasExecutionOutput && IsExecutionSuccessful;

    public bool IsExecutionIncomplete => !IsExecutionComplete;

    public bool CanContinueFromExecution => IsExecutionComplete;

    public bool HasPrimaryQuickAction => PrimaryQuickActionCheck is not null;

    public string PrimaryQuickActionLabel => PrimaryQuickActionCheck?.QuickActionLabel ?? _resources.GetString("QuickFixUnavailableAction");

    public string PrimaryQuickActionTitle => PrimaryQuickActionCheck?.Title ?? _resources.GetString("QuickFixUnavailableTitle");

    public string PrimaryQuickActionDescription => PrimaryQuickActionCheck?.Resolution ?? _resources.GetString("QuickFixUnavailableDescription");

    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        RefreshCommand.NotifyCanExecuteChanged();

        try
        {
            HostInspectionSnapshot snapshot = await _hostInspectionService.InspectAsync(CancellationToken.None);
            ApplySnapshot(snapshot);
        }
        catch
        {
            ApplyFailureState();
        }
        finally
        {
            IsLoading = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        BrowseIsoCommand.NotifyCanExecuteChanged();
        BrowseVhdPathCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        ExecuteVmPlanCommand.NotifyCanExecuteChanged();
        UpdateExistingVmCommand.NotifyCanExecuteChanged();
        NotifyStepStateChanged();
    }

    partial void OnIsExecutingPlanChanged(bool value)
    {
        BrowseIsoCommand.NotifyCanExecuteChanged();
        BrowseVhdPathCommand.NotifyCanExecuteChanged();
        ExecuteVmPlanCommand.NotifyCanExecuteChanged();
        UpdateExistingVmCommand.NotifyCanExecuteChanged();
    }

    partial void OnCpuCoresTextChanged(string value) => _appStateService.SetString("CpuCoresText", value);

    partial void OnDiskSizeGbTextChanged(string value) => _appStateService.SetString("DiskSizeGbText", value);

    partial void OnGpuSharePercentageTextChanged(string value) => _appStateService.SetString("GpuSharePercentageText", value);

    partial void OnIsAutoLogonEnabledChanged(bool value) => _appStateService.SetBoolean("IsAutoLogonEnabled", value);

    partial void OnMemoryGbTextChanged(string value) => _appStateService.SetString("MemoryGbText", value);

    partial void OnSelectedGpuNameChanged(string value) => _appStateService.SetString("SelectedGpuName", value);

    partial void OnSelectedNetworkSwitchChanged(string value) => _appStateService.SetString("SelectedNetworkSwitch", value);

    partial void OnSelectedThemeChanged(string value) => _appStateService.SetString("SelectedTheme", value);

    partial void OnSourcePathChanged(string value) => _appStateService.SetString("SourcePath", value);

    partial void OnUsernameChanged(string value) => _appStateService.SetString("Username", value);

    partial void OnVhdPathChanged(string value) => _appStateService.SetString("VhdPath", value);

    partial void OnVmNameChanged(string value) => _appStateService.SetString("VmName", value);

    partial void OnHasExecutionOutputChanged(bool value) => NotifyStepStateChanged();

    partial void OnIsExecutionSuccessfulChanged(bool value) => NotifyStepStateChanged();

    partial void OnValidationSummaryChanged(string value) => OnPropertyChanged(nameof(HasValidationIssues));

    private void ApplyFailureState()
    {
        Checks.Clear();
        GpuDevices.Clear();
        GpuSelectionOptions.Clear();
        GpuSelectionOptions.Add("AUTO");
        NetworkSwitches.Clear();
        NetworkSwitches.Add("Default Switch");

        Checks.Add(
            new HostCheckResult(
                _resources.GetString("InspectionFailedTitle"),
                _resources.GetString("CheckStateError"),
                _resources.GetString("InspectionFailedDescription"),
                _resources.GetString("InspectionFailedResolution"),
                HostCheckState.Error,
                HostCheckActionKind.RefreshChecks,
                _resources.GetString("QuickFixRetryInspection")));

        ReadyCount = 0;
        WarningCount = 0;
        ErrorCount = 1;
        HasSnapshot = true;
        StatusSummary = string.Format(
            CultureInfo.CurrentCulture,
            _resources.GetString("SummaryWithIssues"),
            ReadyCount,
            WarningCount,
            ErrorCount);

        _latestSnapshot = null;
        NotifyStepStateChanged();
        NotifyQuickActionStateChanged();
    }

    private void ApplySnapshot(HostInspectionSnapshot snapshot)
    {
        Checks.Clear();
        GpuDevices.Clear();
        NetworkSwitches.Clear();

        foreach (HostCheckResult check in snapshot.Checks)
        {
            Checks.Add(check);
        }

        foreach (GpuDeviceInfo gpu in snapshot.GpuDevices)
        {
            GpuDevices.Add(gpu);
        }

        foreach (string networkSwitch in snapshot.NetworkSwitches)
        {
            AddNetworkSwitchOption(networkSwitch);
        }

        AddNetworkSwitchOption("Default Switch");

        ReadyCount = snapshot.Checks.Count(check => check.State == HostCheckState.Ready);
        WarningCount = snapshot.Checks.Count(check => check.State == HostCheckState.Warning);
        ErrorCount = snapshot.Checks.Count(check => check.State == HostCheckState.Error);
        OperatingSystemText = snapshot.OperatingSystemDescription;
        ArchitectureText = snapshot.Architecture;
        LastUpdatedText = string.Format(CultureInfo.CurrentCulture, _resources.GetString("LastUpdatedFormat"), snapshot.CapturedAt.ToString("g", CultureInfo.CurrentCulture));
        HasSnapshot = true;
        StatusSummary = ErrorCount == 0 && WarningCount == 0
            ? _resources.GetString("SummaryAllClear")
            : string.Format(CultureInfo.CurrentCulture, _resources.GetString("SummaryWithIssues"), ReadyCount, WarningCount, ErrorCount);

        _latestSnapshot = snapshot;
        ApplyHostDefaults(snapshot);
        NotifyStepStateChanged();
        NotifyQuickActionStateChanged();
    }

    private void ApplyHostDefaults(HostInspectionSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(SelectedNetworkSwitch))
        {
            SelectedNetworkSwitch = snapshot.NetworkSwitches.FirstOrDefault(static switchName => string.Equals(switchName, "Default Switch", StringComparison.OrdinalIgnoreCase))
                ?? snapshot.NetworkSwitches.FirstOrDefault()
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(SelectedGpuName) || string.Equals(SelectedGpuName, "AUTO", StringComparison.OrdinalIgnoreCase))
        {
            SelectedGpuName = "AUTO";
        }
    }

    private void BuildVmPlan()
    {
        VmExecutionPlan plan = _vmExecutionPlanBuilder.Build(CreateDraft(), _latestSnapshot);

        VmPlanIssues.Clear();

        foreach (VmPlanIssue issue in plan.Issues)
        {
            VmPlanIssues.Add(issue);
        }

        VmPlanSummary = plan.Summary;
        VmPlanIssuesText = string.Join(Environment.NewLine, plan.Issues.Select(static issue => $"{issue.SeverityLabel} {issue.Message}"));
        ValidationSummary = plan.HasBlockingIssues
            ? VmPlanIssuesText
            : string.Empty;
        CreateVmParametersPreview = plan.CreateVmParametersPreview;
        DriverUpdateCommandPreview = plan.UpdateDriverCommandPreview;
        HasExecutionPlan = true;
        _latestPlan = plan;
        ExecutionStatus = plan.HasBlockingIssues
            ? _resources.GetString("VmExecutionBlocked")
            : _resources.GetString("VmExecutionReady");
        ExecutionLog = _resources.GetString("VmExecutionNotRun");
        ExecutionStages.Clear();
        _executionStagesBuffer.Clear();
        ExecutionStagesText = _resources.GetString("VmExecutionNotRun");
        HasExecutionOutput = false;
        IsExecutionSuccessful = false;
        ExecuteVmPlanCommand.NotifyCanExecuteChanged();
        UpdateExistingVmCommand.NotifyCanExecuteChanged();
        NotifyFieldValidationStateChanged();
        NotifyStepStateChanged();
    }

    private bool CanExecuteVmPlan()
    {
        return !IsLoading && !IsExecutingPlan && _latestPlan is { HasBlockingIssues: false };
    }

    private bool CanBrowsePaths()
    {
        return !IsExecutingPlan;
    }

    private async Task BrowseIsoAsync()
    {
        string? path = await _filePickerService.PickIsoFileAsync(CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(path))
        {
            SourcePath = path;
        }
    }

    private async Task BrowseVhdPathAsync()
    {
        string? path = await _filePickerService.PickVhdFolderAsync(CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(path))
        {
            VhdPath = path;
        }
    }

    private async Task ExecuteCheckQuickActionAsync(HostCheckResult? check)
    {
        if (check is null || !check.HasQuickAction)
        {
            return;
        }

        if (check.QuickAction == HostCheckActionKind.RefreshChecks)
        {
            await RefreshAsync();
            return;
        }

        await _hostQuickActionService.ExecuteAsync(check.QuickAction, CancellationToken.None);
    }

    private Task ExecutePrimaryQuickActionAsync()
    {
        return ExecuteCheckQuickActionAsync(PrimaryQuickActionCheck);
    }

    private HostCheckResult? PrimaryQuickActionCheck => Checks.FirstOrDefault(static check => check.HasQuickAction && check.State != HostCheckState.Ready);

    private void NotifyQuickActionStateChanged()
    {
        OnPropertyChanged(nameof(HasPrimaryQuickAction));
        OnPropertyChanged(nameof(PrimaryQuickActionLabel));
        OnPropertyChanged(nameof(PrimaryQuickActionTitle));
        OnPropertyChanged(nameof(PrimaryQuickActionDescription));
        ExecutePrimaryQuickActionCommand.NotifyCanExecuteChanged();
    }

    private async Task ExecuteVmPlanAsync()
    {
        if (_latestPlan is null || _latestPlan.HasBlockingIssues)
        {
            return;
        }

        IsExecutingPlan = true;
        ExecutionStatus = _resources.GetString("VmExecutionRunning");
        ExecutionLog = string.Empty;
        HasExecutionOutput = true;
        SeedExecutionStages();

        try
        {
            IProgress<VmExecutionStageResult> progress = new Progress<VmExecutionStageResult>(ApplyExecutionStageUpdate);
            IProgress<string> outputProgress = new Progress<string>(AppendExecutionLogLine);
            VmExecutionResult result = await _vmProvisioningService.ExecuteCreateVmAsync(CreateDraft(), progress, outputProgress, CancellationToken.None);

            foreach (VmExecutionStageResult stage in result.Stages)
            {
                ApplyExecutionStageUpdate(stage);
            }

            ExecutionStatus = result.Summary;
            ExecutionLog = result.Output;
            IsExecutionSuccessful = result.IsSuccess;
            HasExecutionOutput = true;
        }
        finally
        {
            IsExecutingPlan = false;
        }
    }

    private async Task UpdateExistingVmAsync()
    {
        if (_latestPlan is null || _latestPlan.HasBlockingIssues)
        {
            return;
        }

        IsExecutingPlan = true;
        ExecutionStatus = _resources.GetString("VmExecutionUpdatingExisting");
        ExecutionLog = string.Empty;
        HasExecutionOutput = true;
        SeedDriverUpdateStages();

        try
        {
            IProgress<VmExecutionStageResult> progress = new Progress<VmExecutionStageResult>(ApplyExecutionStageUpdate);
            IProgress<string> outputProgress = new Progress<string>(AppendExecutionLogLine);
            VmExecutionResult result = await _vmProvisioningService.ExecuteUpdateExistingVmAsync(CreateDraft(), progress, outputProgress, CancellationToken.None);

            foreach (VmExecutionStageResult stage in result.Stages)
            {
                ApplyExecutionStageUpdate(stage);
            }

            ExecutionStatus = result.Summary;
            ExecutionLog = result.Output;
            IsExecutionSuccessful = result.IsSuccess;
            HasExecutionOutput = true;
        }
        finally
        {
            IsExecutingPlan = false;
        }
    }

    private VmConfigurationDraft CreateDraft()
    {
        return new VmConfigurationDraft
        {
            CpuCoresText = CpuCoresText,
            DiskSizeGbText = DiskSizeGbText,
            GpuName = SelectedGpuName,
            GpuSharePercentageText = GpuSharePercentageText,
            IsAutoLogonEnabled = IsAutoLogonEnabled,
            MemoryGbText = MemoryGbText,
            NetworkSwitchName = SelectedNetworkSwitch,
            Password = Password,
            SourcePath = SourcePath,
            SupportsNamedGpuSelection = _latestSnapshot is not null && !_latestSnapshot.OperatingSystemDescription.Contains("Windows 10", StringComparison.OrdinalIgnoreCase),
            Username = Username,
            VhdPath = VhdPath,
            VmName = VmName,
        };
    }

    private void OnGpuDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshGpuSelectionOptions();
        OnPropertyChanged(nameof(HasGpuDevices));
    }

    private void OnNetworkSwitchesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNetworkSwitches));
        OnPropertyChanged(nameof(ShowNetworkSwitchFallback));
    }

    private void AddNetworkSwitchOption(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !NetworkSwitches.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            NetworkSwitches.Add(value);
        }
    }

    private bool HasIssue(params string[] terms)
    {
        return VmPlanIssues.Any(issue => issue.Severity == VmPlanIssueSeverity.Error && terms.Any(term => issue.Message.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private void NotifyFieldValidationStateChanged()
    {
        OnPropertyChanged(nameof(HasVmNameIssue));
        OnPropertyChanged(nameof(HasUsernameIssue));
        OnPropertyChanged(nameof(HasPasswordIssue));
        OnPropertyChanged(nameof(HasIsoPathIssue));
        OnPropertyChanged(nameof(HasVhdPathIssue));
        OnPropertyChanged(nameof(HasCpuIssue));
        OnPropertyChanged(nameof(HasMemoryIssue));
        OnPropertyChanged(nameof(HasDiskSizeIssue));
        OnPropertyChanged(nameof(HasGpuShareIssue));
        OnPropertyChanged(nameof(HasNetworkSwitchIssue));
        OnPropertyChanged(nameof(HasGpuIssue));
    }

    private void RefreshGpuSelectionOptions()
    {
        string currentSelection = SelectedGpuName;

        GpuSelectionOptions.Clear();
        GpuSelectionOptions.Add("AUTO");

        foreach (string gpuName in GpuDevices.Select(static gpu => gpu.Name).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            GpuSelectionOptions.Add(gpuName);
        }

        SelectedGpuName = GpuSelectionOptions.Contains(currentSelection, StringComparer.OrdinalIgnoreCase)
            ? currentSelection
            : "AUTO";
    }

    private void SeedExecutionStages()
    {
        lock (_executionStagesLock)
        {
            ExecutionStages.Clear();
            _executionStagesBuffer.Clear();

            VmExecutionStageResult[] pendingStages =
            [
                new(_resources.GetString("VmExecutionStagePrepareTitle"), VmExecutionStageState.Pending, _resources.GetString("VmExecutionStagePending"), _resources.GetString("VmExecutionStagePreparePending")),
                new(_resources.GetString("VmExecutionStageCreateTitle"), VmExecutionStageState.Pending, _resources.GetString("VmExecutionStagePending"), _resources.GetString("VmExecutionStageCreatePending")),
                new(_resources.GetString("VmExecutionStageDriverTitle"), VmExecutionStageState.Pending, _resources.GetString("VmExecutionStagePending"), _resources.GetString("VmExecutionStageDriverPending")),
            ];

            foreach (VmExecutionStageResult stage in pendingStages)
            {
                _executionStagesBuffer.Add(stage);
                ExecutionStages.Add(stage);
            }

            RefreshExecutionStagesText();
        }
    }

    private void SeedDriverUpdateStages()
    {
        lock (_executionStagesLock)
        {
            ExecutionStages.Clear();
            _executionStagesBuffer.Clear();

            VmExecutionStageResult[] pendingStages =
            [
                new(_resources.GetString("VmExecutionStagePrepareTitle"), VmExecutionStageState.Pending, _resources.GetString("VmExecutionStagePending"), _resources.GetString("VmExecutionStagePreparePending")),
                new(_resources.GetString("VmExecutionStageDriverTitle"), VmExecutionStageState.Pending, _resources.GetString("VmExecutionStagePending"), _resources.GetString("VmExecutionStageDriverPending")),
            ];

            foreach (VmExecutionStageResult stage in pendingStages)
            {
                _executionStagesBuffer.Add(stage);
                ExecutionStages.Add(stage);
            }

            RefreshExecutionStagesText();
        }
    }

    private void ApplyExecutionStageUpdate(VmExecutionStageResult update)
    {
        lock (_executionStagesLock)
        {
            int index = _executionStagesBuffer.FindIndex(stage => string.Equals(stage.Title, update.Title, StringComparison.Ordinal));

            if (index < 0)
            {
                _executionStagesBuffer.Add(update);
                ExecutionStages.Add(update);
            }
            else
            {
                _executionStagesBuffer[index] = update;
                ExecutionStages[index] = update;
            }

            RefreshExecutionStagesText();
        }
    }

    private void AppendExecutionLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string normalizedLine = line.Trim();
        ExecutionLog = string.IsNullOrWhiteSpace(ExecutionLog)
            ? normalizedLine
            : $"{ExecutionLog}{Environment.NewLine}{normalizedLine}";

        ApplyScriptLineToCurrentStage(normalizedLine);
    }

    private void ApplyScriptLineToCurrentStage(string line)
    {
        const string infoPrefix = "INFO   : ";

        if (!line.StartsWith(infoPrefix, StringComparison.Ordinal))
        {
            return;
        }

        string message = line[infoPrefix.Length..];
        string stageTitle = message.Contains("driver", StringComparison.OrdinalIgnoreCase) || message.Contains("VBCable", StringComparison.OrdinalIgnoreCase)
            ? _resources.GetString("VmExecutionStageDriverTitle")
            : _resources.GetString("VmExecutionStageCreateTitle");

        ApplyExecutionStageUpdate(new VmExecutionStageResult(stageTitle, VmExecutionStageState.Running, _resources.GetString("VmExecutionStageRunning"), message));
    }

    private void RefreshExecutionStagesText()
    {
        ExecutionStagesText = string.Join(
            Environment.NewLine,
            _executionStagesBuffer.Select(static stage => $"{stage.Title}: {stage.StatusLabel} {stage.Message}"));
    }

    private void NotifyStepStateChanged()
    {
        OnPropertyChanged(nameof(IsOverviewComplete));
        OnPropertyChanged(nameof(IsOverviewIncomplete));
        OnPropertyChanged(nameof(CanContinueFromOverview));
        OnPropertyChanged(nameof(IsConfigurationComplete));
        OnPropertyChanged(nameof(IsConfigurationIncomplete));
        OnPropertyChanged(nameof(CanContinueFromConfiguration));
        OnPropertyChanged(nameof(IsExecutionComplete));
        OnPropertyChanged(nameof(IsExecutionIncomplete));
        OnPropertyChanged(nameof(CanContinueFromExecution));
    }

}