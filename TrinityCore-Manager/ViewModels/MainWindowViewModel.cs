﻿// TrinityCore-Manager
// Copyright (C) 2013 Mitchell Kutchuk
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Catel.Data;
using Catel.MVVM;
using Catel.MVVM.Services;
using Ookii.Dialogs.Wpf;
using TrinityCore_Manager.Clients;
using TrinityCore_Manager.Database.Classes;
using TrinityCore_Manager.Database.Enums;
using TrinityCore_Manager.Exceptions;
using TrinityCore_Manager.Extensions;
using TrinityCore_Manager.Misc;
using TrinityCore_Manager.Models;
using TrinityCore_Manager.Properties;
using TrinityCore_Manager.TC;
using TrinityCore_Manager.TCM;
using TrinityCore_Manager.Views;

namespace TrinityCore_Manager.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {

        private readonly IUIVisualizerService _uiVisualizerService;
        private readonly IPleaseWaitService _pleaseWaitService;
        private readonly IMessageService _messageService;
        private readonly DispatcherService _dispatcherService;

        private bool _isCloning;
        private bool _isCompiling;

        private CancellationTokenSource _compilerCts;

        private DispatcherTimer _backupTimer;

        public Command ExecuteConsoleCommand { get; private set; }

        public Command StartServerCommand { get; private set; }

        public Command StopServerCommand { get; private set; }

        public Command OpenConfigurationCommand { get; private set; }

        public Command EditSettingsCommand { get; private set; }

        public Command DownloadUpdateTCCommand { get; private set; }

        public Command CompileCommand { get; private set; }

        public Command SelectCharacterCommand { get; private set; }

        public Command SetTrunkLocationCommand { get; private set; }

        public Command OpenSetupWizardCommand { get; private set; }

        public Command BackupDatabaseCommand { get; private set; }

        public Command RestoreDatabaseCommand { get; private set; }

        public Command FindItemCommand { get; private set; }

        #region Character Commands

        public Command ReviveCharCommand { get; private set; }

        public Command ForceRenameCommand { get; private set; }

        public Command BanCharCommand { get; private set; }

        public Command CharCustomizeCommand { get; private set; }

        public Command CharRaceChangeCommand { get; private set; }

        public Command CharFactionChangeCommand { get; private set; }

        public Command CharChangeLevelCommand { get; private set; }

        public Command ShowPlayerInfoCommand { get; private set; }

        #endregion

        public Command SendMessageCommand { get; private set; }

        public Command IPManagementCommand { get; private set; }

        public Command AccountManagementCommand { get; private set; }

        public Command AddAccountCommand { get; private set; }

        public Command EditAccountCommand { get; private set; }

        public Command DatabaseAccountCleanupCommand { get; private set; }

        public Command ContactUsCommand { get; private set; }

        public MainWindowViewModel(IUIVisualizerService uiVisualizerService, IPleaseWaitService pleaseWaitService, IMessageService messageService)
        {

            _uiVisualizerService = uiVisualizerService;
            _pleaseWaitService = pleaseWaitService;
            _messageService = messageService;

            _dispatcherService = GetService<DispatcherService>();

            Busy = false;

            _isCloning = false;
            _isCompiling = false;

            BusyProgress = 0.0;

            CompilePlatforms = new ObservableCollection<string>
            {
                "x86",
                "x64"
            };

            ExecuteConsoleCommand = new Command(ExecConsoleCommand);
            StartServerCommand = new Command(StartServer);
            StopServerCommand = new Command(StopServer);
            EditSettingsCommand = new Command(EditSettings);
            DownloadUpdateTCCommand = new Command(DownloadUpdateTC);
            CompileCommand = new Command(Compile);
            SelectCharacterCommand = new Command(SelectCharacter);
            SetTrunkLocationCommand = new Command(SetTrunkLocation);
            OpenConfigurationCommand = new Command(OpenConfiguration);
            OpenSetupWizardCommand = new Command(OpenSetupWizard);
            BackupDatabaseCommand = new Command(BackupDatabase);
            RestoreDatabaseCommand = new Command(RestoreDatabase);
            FindItemCommand = new Command(FindItem);
            ReviveCharCommand = new Command(ReviveChar);
            ForceRenameCommand = new Command(ForceRename);
            BanCharCommand = new Command(BanChar);
            CharCustomizeCommand = new Command(CharCustomize);
            CharRaceChangeCommand = new Command(CharRaceChange);
            CharFactionChangeCommand = new Command(CharFactionChange);
            CharChangeLevelCommand = new Command(CharChangeLevel);
            ShowPlayerInfoCommand = new Command(ShowPlayerInfo);
            SendMessageCommand = new Command(SendMessage);
            IPManagementCommand = new Command(IPManagement);
            AccountManagementCommand = new Command(AccountManagement);
            AddAccountCommand = new Command(AddAccount);
            EditAccountCommand = new Command(ShowEditAccount);
            DatabaseAccountCleanupCommand = new Command(ShowDatabaseAccountCleanup);
            ContactUsCommand = new Command(ShowContactUs);

            Characters = new ObservableCollection<string>();

            CheckSettings();
            InitBackupTimer();

            SetColorTheme(Settings.Default.ColorTheme);

            Application.Current.Exit += Current_Exit;
            Application.Current.MainWindow.MinHeight = 744;
            Application.Current.MainWindow.MinWidth = 1137;
        }

        private void ShowContactUs()
        {

            ContactUsModel model = new ContactUsModel();

            _uiVisualizerService.ShowDialog(new ContactUsViewModel(model, _pleaseWaitService, _messageService));

        }

        private void ShowDatabaseAccountCleanup()
        {

            DatabaseAccountCleanupModel dacm = new DatabaseAccountCleanupModel();

            _uiVisualizerService.ShowDialog(new DatabaseAccountCleanupViewModel(dacm, _pleaseWaitService, _messageService));

        }

        private void ShowEditAccount()
        {

            EditAccountModel model = new EditAccountModel();

            _uiVisualizerService.ShowDialog(new EditAccountViewModel(model, _pleaseWaitService, _messageService));

        }

        private void AddAccount()
        {

            AddAccountModel model = new AddAccountModel();

            _uiVisualizerService.ShowDialog(new AddAccountViewModel(model, _pleaseWaitService, _messageService));

        }

        private async void AccountManagement()
        {

            List<AccountManagementModel> accountList = new List<AccountManagementModel>();

            List<BannedAccount> accts = await TCManager.Instance.AuthDatabase.GetBannedAccounts();

            foreach (var acct in accts)
            {

                var account = await TCManager.Instance.AuthDatabase.GetAccount(acct.Id);
                var ban = await TCManager.Instance.AuthDatabase.GetBannedAccount(acct.Id);

                accountList.Add(new AccountManagementModel(account.Username, ban.BanDate, ban.BanReason));

            }

            AccountsManagementModel acctsModel = new AccountsManagementModel(accountList);

            _uiVisualizerService.ShowDialog(new AccountManagementViewModel(acctsModel, _uiVisualizerService, _pleaseWaitService, _messageService));

        }

        private async void IPManagement()
        {

            List<IPModel> ips = new List<IPModel>();

            List<IPBan> ipBans = await TCManager.Instance.AuthDatabase.GetIPBans();

            ips.AddRange(ipBans.Select(p => new IPModel() { IPAddress = p.IP }));


            IPManagementModel model = new IPManagementModel(ips);

            _uiVisualizerService.ShowDialog(new IPManagementViewModel(model, _uiVisualizerService, _pleaseWaitService, _messageService));

        }

        private async void SendMessage()
        {

            if (!string.IsNullOrEmpty(MessageText))
            {

                if (TCManager.Instance.Online)
                {

                    if (AnnouncementSelected)
                        await TCAction.AnnounceToServer(MessageText);
                    else if (ServerNotificationSelected)
                        await TCAction.NotifiyServer(MessageText);
                    else if (GMAnnouncementSelected)
                        await TCAction.NotifyGMs(MessageText);


                }

                MessageText = String.Empty;

            }

        }

        private async void ShowPlayerInfo()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            TCCharacter c = await TCManager.Instance.CharDatabase.GetCharacter(SelectedCharacter);

            if (c != null)
            {

                Account acct = await TCManager.Instance.AuthDatabase.GetAccount(c.Account);

                if (acct != null)
                {

                    GMLevel gmLvl = await TCManager.Instance.AuthDatabase.GetAccountAccess(c.Account);

                    string gmLevelStr;

                    switch (gmLvl)
                    {

                        case GMLevel.Moderator:

                            gmLevelStr = "Moderator";

                            break;

                        case GMLevel.GM:

                            gmLevelStr = "GM";

                            break;

                        case GMLevel.HeadGM:

                            gmLevelStr = "Head GM";

                            break;

                        case GMLevel.Admin:

                            gmLevelStr = "Admin";

                            break;

                        default:

                            gmLevelStr = "Player";

                            break;

                    }

                    int gold = c.Money / 10000;
                    int silver = (c.Money % 10000) / 100;
                    int copper = (c.Money % 10000) % 100;

                    string money = String.Format("{0} Gold {1} Silver {2} Copper", gold, silver, copper);

                    PlayerInformationModel model = new PlayerInformationModel
                    {
                        CharacterName = c.Name,
                        AccountId = c.Account.ToString(),
                        AccountName = acct.Username,
                        Class = c.Class.GetCharacterClassName(),
                        Email = acct.Email,
                        GMLevel = gmLevelStr,
                        LastIp = acct.LastIp,
                        LastLogin = acct.LastLogin.ToString(),
                        Level = c.Level.ToString(),
                        Money = money,
                        PlayedTime = TimeSpan.FromSeconds(c.TotalTime).ToReadableString(),
                        Race = c.Race.GetCharacterRaceName(),
                        TotalKills = c.TotalKills.ToString(),
                    };

                    _uiVisualizerService.ShowDialog(new PlayerInformationViewModel(model));

                }

            }

        }

        private async void CharChangeLevel()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            await TCAction.ModifyAccountLevel(SelectedCharacter, CharLevel);

        }

        private async void CharFactionChange()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            try
            {
                await TCAction.RequestChangeFaction(SelectedCharacter);
            }
            catch (ServerOfflineException ex)
            {
                _messageService.ShowError(ex.Message);
            }

        }

        private async void CharRaceChange()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            try
            {
                await TCAction.RequestChangeRace(SelectedCharacter);
            }
            catch (ServerOfflineException ex)
            {
                _messageService.ShowError(ex.Message);
            }

        }

        private async void CharCustomize()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            try
            {
                await TCAction.CustomizeCharacter(SelectedCharacter);
            }
            catch (ServerOfflineException ex)
            {
                _messageService.ShowError(ex.Message);
            }

        }

        private async void BanChar()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            try
            {
                await TCAction.BanCharacter(SelectedCharacter);
            }
            catch (ServerOfflineException ex)
            {
                _messageService.ShowError(ex.Message);
            }

        }

        private async void ForceRename()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            try
            {
                await TCAction.ForceCharRename(SelectedCharacter);
            }
            catch (ServerOfflineException ex)
            {
                _messageService.ShowError(ex.Message);
            }

        }

        private async void ReviveChar()
        {

            if (string.IsNullOrEmpty(SelectedCharacter))
            {

                _messageService.ShowError("No character selected!");

                return;

            }

            try
            {
                await TCAction.RevivePlayer(SelectedCharacter);
            }
            catch (ServerOfflineException ex)
            {
                _messageService.ShowError(ex.Message);
            }

        }

        private void FindItem()
        {

            var fi = new FindItemModel();

            var returnVal = _uiVisualizerService.ShowDialog(new FindItemViewModel(fi, _uiVisualizerService, _pleaseWaitService, _messageService));

        }

        private void BackupDatabase()
        {

            var bm = new BackupDatabaseModel
            {
                AuthSelected = Settings.Default.BackupScheduleAuth,
                CharSelected = Settings.Default.BackupScheduleChar,
                WorldSelected = Settings.Default.BackupScheduleWorld,
                BackupsScheduled = Settings.Default.BackupScheduleAuth || Settings.Default.BackupScheduleChar || Settings.Default.BackupScheduleWorld,
                BackupDays = Settings.Default.BackupDays,
                BackupHours = Settings.Default.BackupHours,
                BackupMinutes = Settings.Default.BackupMinutes
            };

            var returnVal = _uiVisualizerService.ShowDialog(new BackupDatabaseViewModel(bm, _uiVisualizerService, _pleaseWaitService, _messageService));

            if (returnVal.HasValue && returnVal.Value)
            {

                Settings.Default.BackupScheduleAuth = bm.AuthSelected;
                Settings.Default.BackupScheduleChar = bm.CharSelected;
                Settings.Default.BackupScheduleWorld = bm.WorldSelected;
                Settings.Default.BackupDays = bm.BackupDays;
                Settings.Default.BackupHours = bm.BackupHours;
                Settings.Default.BackupMinutes = bm.BackupMinutes;

                Settings.Default.Save();

                InitBackupTimer();

            }

        }

        public void RestoreDatabase()
        {

            var rd = new RestoreDatabaseModel
            {
                AuthDatabase = Settings.Default.DBAuthName,
                CharDatabase = Settings.Default.DBCharName,
                WorldDatabase = Settings.Default.DBWorldName,
            };

            var returnVal = _uiVisualizerService.ShowDialog(new RestoreDatabaseViewModel(rd, _uiVisualizerService, _pleaseWaitService, _messageService));

        }

        private async void Compile()
        {

            if (String.IsNullOrEmpty(Settings.Default.TrunkLocation))
            {
                if (_messageService.Show("You must first set your trunk location! Want to do this right now?", "Trunk location not set!", MessageButton.YesNo, MessageImage.Question) == MessageResult.Yes)
                    SetTrunkLocation();

                return;
            }

            if (_isCloning)
            {

                _messageService.ShowError("Cloning in progress. Please wait until this has finished!");

                return;

            }

            if (_isCompiling)
            {

                if (_compilerCts != null)
                    _compilerCts.Cancel();

                _messageService.ShowError("Compiling has been aborted!");

                return;

            }

            if (Busy)
            {

                _messageService.ShowError("TrinityCore Manager is currently busy. Please wait.");

                return;

            }

            bool x64 = CompilePlatform.Equals("x64", StringComparison.OrdinalIgnoreCase);

            if (x64 && !Environment.Is64BitOperatingSystem)
            {

                _messageService.ShowError("Your operating system is not 64 bit!");

                return;

            }

            _compilerCts = new CancellationTokenSource();

            OutputText = String.Empty;

            var progress = new Progress<string>(prog => _dispatcherService.BeginInvoke(() =>
            {
                OutputText += prog + Environment.NewLine;
            }));

            Busy = true;
            BusyIndeterminate = true;

            _isCompiling = true;

            string temp = FileHelper.GenerateTempDirectory();

            bool result = await CMake.Generate(Settings.Default.TrunkLocation, temp, x64, progress, _compilerCts.Token);


            if (result)
            {

                _compilerCts = new CancellationTokenSource();

                result = await TCCompiler.Compile(temp, x64, progress, _compilerCts.Token);

                if (result)
                    FileHelper.CopyDirectory(Path.Combine(temp, "bin", "Release"), Settings.Default.ServerFolder);
                else
                    _messageService.ShowError("Compile has failed!");

                FileHelper.DeleteDirectory(temp);

            }
            else
            {
                _messageService.ShowError("Compile has failed!");
            }

            Busy = false;
            BusyIndeterminate = false;

            _isCompiling = false;

        }

        private void SetTrunkLocation()
        {
            var dialog = new VistaFolderBrowserDialog();
            dialog.SelectedPath = Settings.Default.TrunkLocation;

            var showDialog = dialog.ShowDialog();

            if (showDialog.HasValue && showDialog.Value)
            {
                Settings.Default.TrunkLocation = dialog.SelectedPath;
                Settings.Default.Save();
            }
        }

        private async void DownloadUpdateTC()
        {
            if (String.IsNullOrEmpty(Settings.Default.TrunkLocation))
            {
                if (_messageService.Show("You must first set your trunk location! Want to do this right now?", "Trunk location not set!", MessageButton.YesNo, MessageImage.Question) == MessageResult.Yes)
                    SetTrunkLocation();

                return;
            }

            if (_isCloning)
            {

                _messageService.ShowError("Cloning is already in progress!");

                return;

            }

            if (_isCompiling)
            {

                _messageService.ShowError("Currently compiling TrinityCore! Please wait until this has finished.");

                return;

            }

            Busy = true;
            BusyIndeterminate = true;

            _isCloning = true;

            if (new DirectoryInfo(Settings.Default.TrunkLocation).GetFiles().Length == 0)
            {

                var progress = new Progress<double>(prog => _dispatcherService.BeginInvoke(() =>
                                                    {

                                                        if (BusyIndeterminate)
                                                            BusyIndeterminate = false;

                                                        if (prog - BusyProgress > 1 || prog >= 99)
                                                        {
                                                            BusyProgress = prog;
                                                        }

                                                    }));

                bool cloneSuccess = await TrinityCoreRepository.Clone(Settings.Default.TrunkLocation, progress);

                _dispatcherService.Invoke(() =>
                {
                    if (cloneSuccess)
                        _messageService.Show("Cloning has been completed!", "Success", MessageButton.OK, MessageImage.Information);
                    else
                        _messageService.Show("Cloning could not be completed!", "Something went wrong", MessageButton.OK, MessageImage.Error);
                });

                _isCloning = false;
            }
            else
            {
                BusyIndeterminate = true;
                OutputText = String.Empty;

                var progress = new Progress<string>(prog => _dispatcherService.BeginInvokeIfRequired(delegate
                {
                    OutputText += prog + Environment.NewLine;
                }));

                bool pullSuccess = await TrinityCoreRepository.Pull(Settings.Default.TrunkLocation, progress);

                _dispatcherService.Invoke(() =>
                {
                    if (pullSuccess)
                        _messageService.Show("The TrinityCore repository has been updated to the latest version.", "Success", MessageButton.OK, MessageImage.Information);
                    else
                        _messageService.Show("Pulling could not be completed!", "Something went wrong", MessageButton.OK, MessageImage.Error);
                });

                _isCloning = false;
            }

            BusyIndeterminate = false;
            BusyProgress = 0;
            Busy = false;

        }

        private void EditSettings()
        {
            var sm = new SettingsModel();
            sm.SelectedTheme = Settings.Default.ColorTheme;

            sm.Themes.Add("Black");
            sm.Themes.Add("Silver");
            sm.Themes.Add("Blue");

            _uiVisualizerService.ShowDialog(new SettingsViewModel(sm), (sender, e) =>
            {
                if (e.Result.HasValue && e.Result.Value)
                    SetColorTheme(sm.SelectedTheme);
            });
        }

        private void SelectCharacter()
        {
            var sm = new CharacterSelectingModel();

            _uiVisualizerService.ShowDialog(new CharacterSelectingViewModel(sm), (sender, e) =>
            {
                if (!Characters.Any(p => p.Equals(sm.SelectedCharacter, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(sm.SelectedCharacter))
                {
                    Characters.Add(sm.SelectedCharacter);
                    SelectedCharacter = sm.SelectedCharacter;
                }
            });
        }

        private void OpenSetupWizard()
        {
            ShowWizard();
        }

        private void SetColorTheme(string colorTheme)
        {
            Application.Current.Resources.BeginInit();
            Application.Current.Resources.MergedDictionaries.RemoveAt(1);

            switch (colorTheme.ToLower())
            {
                case "black":
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Fluent;component/Themes/Office2010/Black.xaml") });
                    break;
                case "silver":
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Fluent;component/Themes/Office2010/Silver.xaml") });
                    break;
                case "blue":
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Fluent;component/Themes/Office2010/Blue.xaml") });
                    break;
            }

            Application.Current.Resources.EndInit();

        }

        void Current_Exit(object sender, ExitEventArgs e)
        {

            var inst = TCManager.Instance;

            if (inst.AuthClient != null && inst.WorldClient != null && (inst.AuthClient.IsOnline || inst.WorldClient.IsOnline))
                StopServer();

        }

        private void InitBackupTimer()
        {

            if (Settings.Default.BackupScheduleAuth || Settings.Default.BackupScheduleChar || Settings.Default.BackupScheduleWorld)
            {

                TCManager.Instance.ScheduleBackups();

                if (_backupTimer != null)
                {
                    _backupTimer.Stop();
                }
                else
                {
                    _backupTimer = new DispatcherTimer();
                }


                _backupTimer.Tick += backupTimer_Tick;
                _backupTimer.Interval = new TimeSpan(0, 0, 1);
                _backupTimer.Start();

                BackupCountingDown = true;

            }
            else
            {

                TCManager.Instance.StopScheduledBackups();

                BackupCountingDown = false;

            }

        }

        private void backupTimer_Tick(object sender, EventArgs e)
        {

            DateTimeOffset dto = TCManager.Instance.BackupNext;

            TimeSpan ts = dto - DateTime.Now;

            BackupText = String.Format("Next backup: {0}", ts.ToReadableString());

        }

        private void ShowWizard(bool exit = false)
        {

            var set = Settings.Default;

            Version ver = Assembly.GetExecutingAssembly().GetName().Version;

            var wm = new WizardModel
            {
                ConnectLocally = (ServerType)set.ServerType == ServerType.Local,
                ConnectRemotely = (ServerType)set.ServerType == ServerType.RemoteAccess,
                ServerFolderLocation = set.ServerFolder,
                Host = set.RAHost,
                Port = set.RAPort,
                Username = set.RAUsername,
                Password = String.Empty,
                MySQLHost = set.DBHost,
                MySQLPort = set.DBPort,
                MySQLUsername = set.DBUsername,
                MySQLPassword = String.Empty,
                SelectedAuthDB = set.DBAuthName,
                SelectedCharDB = set.DBCharName,
                SelectedWorldDB = set.DBWorldName,
                //TCMVersion = string.Format("v{0}.{1}", ver.Major, ver.Minor)
            };

            var wizardView = new SetupWizardViewModel(wm, _uiVisualizerService, _pleaseWaitService, _messageService);

            var result = _uiVisualizerService.ShowDialog(wizardView);

            if (result.HasValue && result.Value)
            {
            }
            else if (exit)
            {
                Application.Current.Shutdown();
            }

        }

        private void CheckSettings()
        {

            var set = Settings.Default;

            if (String.IsNullOrEmpty(set.DBHost) || String.IsNullOrEmpty(set.DBUsername) || String.IsNullOrEmpty(set.DBPassword) || String.IsNullOrEmpty(set.DBAuthName) ||
                String.IsNullOrEmpty(set.DBCharName) || String.IsNullOrEmpty(set.DBWorldName))
            {
                ShowWizard(true);
            }
            else if ((ServerType)set.ServerType == ServerType.Local)
            {
                if (String.IsNullOrEmpty(set.ServerFolder))
                {
                    ShowWizard(true);
                }
            }
            else if ((ServerType)set.ServerType == ServerType.RemoteAccess)
            {
                if (String.IsNullOrEmpty(set.RAUsername) || String.IsNullOrEmpty(set.RAPassword))
                {
                    ShowWizard(true);
                }
            }

        }

        private void StartServer()
        {

            TCManager inst = TCManager.Instance;

            if (!File.Exists(Path.Combine(Settings.Default.ServerFolder, "authserver.exe")))
            {

                _messageService.ShowError(new Exception("The file 'authserver.exe' could not be found!"));

                return;

            }

            if (!File.Exists(Path.Combine(Settings.Default.ServerFolder, "worldserver.exe")))
            {

                _messageService.ShowError(new Exception("The file 'worldserver.exe' could not be found!"));

                return;

            }

            if (inst.AuthClient != null)
            {
                if (inst.AuthClient.IsOnline)
                {

                    MessageResult result = _messageService.Show("Authserver is already running! Kill it?", "Kill it?", MessageButton.YesNo, MessageImage.Question);

                    if (result == MessageResult.No)
                        return;

                    ProcessHelper.KillProcess(((LocalClient)inst.AuthClient).UnderlyingProcessId);

                }
            }

            if (inst.WorldClient != null)
            {
                if (inst.WorldClient.IsOnline)
                {

                    MessageResult result = _messageService.Show("Worldserver is already running! Kill it?", "Kill it?", MessageButton.YesNo, MessageImage.Question);

                    if (result == MessageResult.No)
                        return;

                    ProcessHelper.KillProcess(((LocalClient)inst.WorldClient).UnderlyingProcessId);

                }
            }

            ConsoleText = String.Empty;

            var authClient = new LocalClient(Path.Combine(Settings.Default.ServerFolder, "authserver.exe"));
            var worldClient = new LocalClient(Path.Combine(Settings.Default.ServerFolder, "worldserver.exe"));

            inst.AuthClient = authClient;
            inst.WorldClient = worldClient;

            AuthOnline = true;
            WorldOnline = true;
            ServerOnline = true;

            inst.AuthClient.Start();
            inst.WorldClient.Start();

            authClient.ClientExited += authClient_ClientExited;
            worldClient.ClientExited += worldClient_ClientExited;

            worldClient.DataReceived += worldClient_DataReceived;

        }

        void worldClient_DataReceived(object sender, ClientReceivedDataEventArgs e)
        {
            ConsoleText += e.Data + Environment.NewLine;
        }

        private void StopServer()
        {
            var inst = TCManager.Instance;

            var authClient = inst.AuthClient;
            var worldClient = inst.WorldClient;

            if (authClient == null)
                throw new NullReferenceException("authClient");

            if (worldClient == null)
                throw new NullReferenceException("worldClient");


            authClient.Stop();
            worldClient.Stop();

        }

        private void OpenConfiguration()
        {
            if (!File.Exists(Path.Combine(Settings.Default.ServerFolder, "worldserver.conf")))
            {
                _messageService.ShowError(new Exception("The file 'worldserver.conf' could not be found!"));
                return;
            }

            try
            {
                Process.Start(Path.Combine(Settings.Default.ServerFolder, "worldserver.conf"));
            }
            catch
            {
                _messageService.ShowError("The config file could not be opened!");
            }
        }

        private void worldClient_ClientExited(object sender, EventArgs e)
        {
            WorldOnline = false;
            ServerOnline = false;
        }

        private void authClient_ClientExited(object sender, EventArgs e)
        {
            AuthOnline = false;
            ServerOnline = false;
        }

        private async void ExecConsoleCommand()
        {

            if (TCManager.Instance.Online)
                await TCAction.ExecuteCommand(ConsoleCommand);

            ConsoleCommand = String.Empty;

        }

        public bool AuthOnline
        {
            get
            {
                return GetValue<bool>(AuthOnlineProperty);
            }
            set
            {
                SetValue(AuthOnlineProperty, value);
            }
        }

        public static readonly PropertyData AuthOnlineProperty = RegisterProperty("AuthOnline", typeof(bool));

        public bool WorldOnline
        {
            get
            {
                return GetValue<bool>(WorldOnlineProperty);
            }
            set
            {
                SetValue(WorldOnlineProperty, value);
            }
        }

        public static readonly PropertyData WorldOnlineProperty = RegisterProperty("WorldOnline", typeof(bool));

        public bool ServerOnline
        {
            get
            {
                return GetValue<bool>(ServerOnlineProperty);
            }
            set
            {
                SetValue(ServerOnlineProperty, value);
            }
        }

        public static readonly PropertyData ServerOnlineProperty = RegisterProperty("ServerOnline", typeof(bool));

        public bool BackupCountingDown
        {
            get
            {
                return GetValue<bool>(BackupCountingDownProperty);
            }
            set
            {
                SetValue(BackupCountingDownProperty, value);
            }
        }

        public static readonly PropertyData BackupCountingDownProperty = RegisterProperty("BackupCountingDown", typeof(bool));

        public string ConsoleCommand
        {
            get
            {
                return GetValue<string>(ConsoleCommandProperty);
            }
            set
            {
                SetValue(ConsoleCommandProperty, value);
            }
        }

        public static readonly PropertyData ConsoleCommandProperty = RegisterProperty("ConsoleCommand", typeof(string));

        public string BackupText
        {
            get
            {
                return GetValue<string>(BackupTextProperty);
            }
            set
            {
                SetValue(BackupTextProperty, value);
            }
        }

        public static readonly PropertyData BackupTextProperty = RegisterProperty("BackupText", typeof(string));

        public string ConsoleText
        {
            get
            {
                return GetValue<string>(ConsoleTextProperty);
            }
            set
            {
                SetValue(ConsoleTextProperty, value);
            }
        }

        public static readonly PropertyData ConsoleTextProperty = RegisterProperty("ConsoleText", typeof(string));

        public string OutputText
        {
            get
            {
                return GetValue<string>(OutputTextProperty);
            }
            set
            {
                SetValue(OutputTextProperty, value);
            }
        }

        public static readonly PropertyData OutputTextProperty = RegisterProperty("OutputText", typeof(string));

        public bool Busy
        {
            get
            {
                return GetValue<bool>(BusyProperty);
            }
            set
            {
                SetValue(BusyProperty, value);
            }
        }

        public static readonly PropertyData BusyProperty = RegisterProperty("Busy", typeof(bool));

        public double BusyProgress
        {
            get
            {
                return GetValue<double>(BusyProgressProperty);
            }
            set
            {
                SetValue(BusyProgressProperty, value);
            }
        }

        public static readonly PropertyData BusyProgressProperty = RegisterProperty("BusyProgress", typeof(double));

        public bool BusyIndeterminate
        {
            get
            {
                return GetValue<bool>(BusyIndeterminateProperty);
            }
            set
            {
                SetValue(BusyIndeterminateProperty, value);
            }
        }

        public static readonly PropertyData BusyIndeterminateProperty = RegisterProperty("BusyIndeterminate", typeof(bool));

        public string CompilePlatform
        {
            get
            {
                return GetValue<string>(CompilePlatformProperty);
            }
            set
            {
                SetValue(CompilePlatformProperty, value);
            }
        }

        public static readonly PropertyData CompilePlatformProperty = RegisterProperty("CompilePlatform", typeof(string));

        public ObservableCollection<string> CompilePlatforms
        {
            get
            {
                return GetValue<ObservableCollection<string>>(CompilePlatformsProperty);
            }
            set
            {
                SetValue(CompilePlatformsProperty, value);
            }
        }

        public static readonly PropertyData CompilePlatformsProperty = RegisterProperty("CompilePlatforms", typeof(ObservableCollection<string>));

        public ObservableCollection<string> Characters
        {
            get
            {
                return GetValue<ObservableCollection<string>>(CharactersProperty);
            }
            set
            {
                SetValue(CharactersProperty, value);
            }
        }

        public static readonly PropertyData CharactersProperty = RegisterProperty("Characters", typeof(ObservableCollection<string>));

        public string SelectedCharacter
        {
            get
            {
                return GetValue<string>(SelectedCharacterProperty);
            }
            set
            {

                TCManager.Instance.CharDatabase.GetCharacter(value).ContinueWith(task =>
                {

                    var character = task.Result;

                    CharLevel = character.Level;

                });

                SetValue(SelectedCharacterProperty, value);

            }
        }

        public static readonly PropertyData SelectedCharacterProperty = RegisterProperty("SelectedCharacter", typeof(string));

        public int CharLevel
        {
            get
            {
                return GetValue<int>(CharLevelProperty);
            }
            set
            {
                SetValue(CharLevelProperty, value);
            }
        }

        public static readonly PropertyData CharLevelProperty = RegisterProperty("CharLevel", typeof(int));

        public bool AnnouncementSelected
        {
            get
            {
                return GetValue<bool>(AnnouncementSelectedProperty);
            }
            set
            {
                SetValue(AnnouncementSelectedProperty, value);
            }
        }

        public static readonly PropertyData AnnouncementSelectedProperty = RegisterProperty("AnnouncementSelected", typeof(bool));

        public bool ServerNotificationSelected
        {
            get
            {
                return GetValue<bool>(ServerNotificationSelectedProperty);
            }
            set
            {
                SetValue(ServerNotificationSelectedProperty, value);
            }
        }

        public static readonly PropertyData ServerNotificationSelectedProperty = RegisterProperty("ServerNotificationSelected", typeof(bool));

        public bool GMAnnouncementSelected
        {
            get
            {
                return GetValue<bool>(GMAnnouncementSelectedProperty);
            }
            set
            {
                SetValue(GMAnnouncementSelectedProperty, value);
            }
        }

        public static readonly PropertyData GMAnnouncementSelectedProperty = RegisterProperty("GMAnnouncementSelected", typeof(bool));

        public string MessageText
        {
            get
            {
                return GetValue<string>(MessageTextProperty);
            }
            set
            {
                SetValue(MessageTextProperty, value);
            }
        }

        public static readonly PropertyData MessageTextProperty = RegisterProperty("MessageText", typeof(string));

    }
}
