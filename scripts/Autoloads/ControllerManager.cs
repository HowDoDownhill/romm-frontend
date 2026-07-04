using Godot;
using System.Collections.Generic;
using System.Linq;

public class ConnectedController
{
    public int GodotDeviceId;
    public string ControllerName;
    public string Guid;
    public int ConnectionOrder;
}

public partial class ControllerManager : Node
{
    private List<ConnectedController> connectedControllers = new List<ConnectedController>();
    private int nextConnectionOrder = 0;

    public override void _Ready()
    {
        Input.JoyConnectionChanged += OnJoyConnectionChanged;
        PopulateAlreadyConnectedControllers();
    }

    private void PopulateAlreadyConnectedControllers()
    {
        foreach (int deviceId in Input.GetConnectedJoypads())
        {
            AddController(deviceId);
        }
    }

    private void OnJoyConnectionChanged(long deviceId, bool connected)
    {
        if (connected)
        {
            AddController((int)deviceId);
        }

        else
        {
            RemoveController((int)deviceId);
        }
    }

    private void AddController(int deviceId)
    {
        if (connectedControllers.Any(c => c.GodotDeviceId == deviceId))
        {
            return;
        }

        var controller = new ConnectedController
        {
            GodotDeviceId = deviceId,
            ControllerName = Input.GetJoyName(deviceId),
            Guid = Input.GetJoyGuid(deviceId),
            ConnectionOrder = nextConnectionOrder++
        };

        connectedControllers.Add(controller);
        GD.Print($"Controller connected: {controller.ControllerName} (Device {deviceId}, SDL index {controller.ConnectionOrder})");
    }

    private void RemoveController(int deviceId)
    {
        var controller = connectedControllers.Find(c => c.GodotDeviceId == deviceId);

        if (controller != null)
        {
            GD.Print($"Controller disconnected: {controller.ControllerName} (Device {deviceId})");
            connectedControllers.Remove(controller);
        }
    }

    public List<ConnectedController> GetConnectedControllers()
    {
        return connectedControllers.OrderBy(c => c.ConnectionOrder).ToList();
    }

    public int GetControllerCount()
    {
        return connectedControllers.Count;
    }
}
