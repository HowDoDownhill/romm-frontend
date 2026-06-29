using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public partial class LoginScreen : Control
{
    [ExportGroup("Common Nodes")]
    [Export] private Button loginButton;
    [Export] private Label errorLabel;

    [ExportGroup("RomM Backend")]
    [Export] private LineEdit rommHostInput;
    [Export] private LineEdit rommUserInput;
    [Export] private LineEdit rommPasswordInput;
    [Export] private LineEdit rommApiKeyInput;

    private AppInstance appInstance; 
    
    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        
        loginButton.Pressed += OnLoginButtonPressed;
        
        PopulateFieldsFromConfig();
        AttemptAutoLogin();
    }

    private void PopulateFieldsFromConfig()
    {
        rommHostInput.Text = appInstance.configManager.RomMHost;
        rommUserInput.Text = appInstance.configManager.RomMUsername;
        rommPasswordInput.Text = appInstance.configManager.RomMPassword;
        rommApiKeyInput.Text = appInstance.configManager.RomMApiKey;
    }

    private async void AttemptAutoLogin()
    {
        if (!string.IsNullOrEmpty(appInstance.configManager.RomMHost) ||
            !string.IsNullOrEmpty(appInstance.configManager.RomMUsername) ||
            !string.IsNullOrEmpty(appInstance.configManager.RomMPassword) ||
            !string.IsNullOrEmpty(appInstance.configManager.RomMApiKey))
        {
            await Task.Delay(100);
            OnLoginButtonPressed();
        }
    }

    private async void OnLoginButtonPressed()
    {
        if (errorLabel != null)
        {
            errorLabel.Text = "Authenticating...";
        }

        loginButton.Disabled = true;

        (bool isSuccess, string errorMessage) result = (false, "An unknown error occurred.");
        
        string host = rommHostInput.Text;
        string username = rommUserInput.Text;
        string password = rommPasswordInput.Text;
        string apiKey = rommApiKeyInput.Text;
        result = await appInstance.rommApi.AuthenticateAsync(username, password, host, apiKey);

        if (result.isSuccess)
        {
            appInstance.configManager.SaveRomMCredentials(host, username, password, apiKey);
            appInstance.configManager.SaveValidLoginLastUsed(true);
        }
        

        if (result.isSuccess)
        {
            if (errorLabel != null)
            {
                errorLabel.Text = "Success!";
            }

            GetTree().ChangeSceneToFile("res://scenes/login/loading_screen.tscn");
        }
        
        else
        {
            if (errorLabel != null)
            {
                errorLabel.Text = result.errorMessage;
            }
        }

        loginButton.Disabled = false;
    }
}
