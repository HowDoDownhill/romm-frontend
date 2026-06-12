using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class LoginScreen : Control
{
    private enum BackendType { RomM, Local, SFTP }

    [ExportGroup("Common Nodes")]
    [Export] private OptionButton _backendSelector;
    [Export] private Button _loginButton;
    [Export] private Label _errorLabel;

    [ExportGroup("RomM Backend")]
    [Export] private Container _romMBackendInfo;
    [Export] private LineEdit _romMHostInput;
    [Export] private LineEdit _romMUsernameInput;
    [Export] private LineEdit _romMPasswordInput;
    [Export] private LineEdit _romMApiKeyInput;

    [ExportGroup("Local Backend")]
    [Export] private Container _localBackendInfo;

    [ExportGroup("SFTP Backend")]
    [Export] private Container _sftpBackendInfo;
    
    private BackendManager _backendManager;
    private ConfigManager _configManager;
    private Dictionary<BackendType, IBackend> _backendImplementations;

    public override void _Ready()
    {
        _backendManager = GetNode<BackendManager>("/root/BackendManager");
        _configManager = GetNode<ConfigManager>("/root/ConfigManager");
        
        _backendImplementations = new Dictionary<BackendType, IBackend>
        {
            { BackendType.RomM, GetNode<RomMAPI>("/root/RomMAPI") }
        };

        _loginButton.Pressed += OnLoginButtonPressed;
        _backendSelector.ItemSelected += OnBackendSelected;

        PopulateFieldsFromConfig();
        AttemptAutoLogin();
    }

    private void PopulateFieldsFromConfig()
    {
        _backendSelector.Select(_configManager.LastUsedBackend);
        OnBackendSelected(_configManager.LastUsedBackend);

        _romMHostInput.Text = _configManager.RomMHost;
        _romMUsernameInput.Text = _configManager.RomMUsername;
        _romMPasswordInput.Text = _configManager.RomMPassword;
        _romMApiKeyInput.Text = _configManager.RomMApiKey;
    }

    private async void AttemptAutoLogin()
    {
        if (!string.IsNullOrEmpty(_configManager.RomMHost) && 
            (!string.IsNullOrEmpty(_configManager.RomMUsername) || !string.IsNullOrEmpty(_configManager.RomMApiKey)))
        {
            await Task.Delay(100);
            OnLoginButtonPressed();
        }
    }

    private void OnBackendSelected(long index)
    {
        var selectedBackend = (BackendType)_backendSelector.GetItemId((int)index);
        
        if (_romMBackendInfo != null) _romMBackendInfo.Visible = selectedBackend == BackendType.RomM;
        if (_localBackendInfo != null) _localBackendInfo.Visible = selectedBackend == BackendType.Local;
        if (_sftpBackendInfo != null) _sftpBackendInfo.Visible = selectedBackend == BackendType.SFTP;

        _loginButton.Text = selectedBackend == BackendType.Local ? "Continue" : "Login";
    }

    private async void OnLoginButtonPressed()
    {
        if (_errorLabel != null) _errorLabel.Text = "Authenticating...";
        _loginButton.Disabled = true;

        var selectedBackendType = (BackendType)_backendSelector.GetSelectedId();

        if (!_backendImplementations.TryGetValue(selectedBackendType, out IBackend backend))
        {
            if (_errorLabel != null) _errorLabel.Text = "This backend is not yet implemented.";
            _loginButton.Disabled = false;
            return;
        }

        (bool isSuccess, string errorMessage) result = (false, "An unknown error occurred.");

        if (selectedBackendType == BackendType.RomM)
        {
            string host = _romMHostInput.Text;
            string username = _romMUsernameInput.Text;
            string password = _romMPasswordInput.Text;
            string apiKey = _romMApiKeyInput.Text;
            result = await backend.AuthenticateAsync(username, password, host, apiKey);

            if (result.isSuccess)
            {
                _configManager.SaveRomMCredentials(host, username, password, apiKey);
                _configManager.SaveLastUsedBackend((int)selectedBackendType);
            }
        }

        if (result.isSuccess)
        {
            _backendManager.ActiveBackend = backend;
            if (_errorLabel != null) _errorLabel.Text = "Success!";
            GetTree().ChangeSceneToFile("res://Scenes/loading_screen.tscn");
        }
        else
        {
            if (_errorLabel != null) _errorLabel.Text = result.errorMessage;
        }

        _loginButton.Disabled = false;
    }
}
