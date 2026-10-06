using Godot;
using System.Collections.Generic;

public class PadMappingTable
{
    private const int UnmappedSource = -1;

    private readonly int[] sourceControlByDestination = new int[PadControls.Count];

    private PadMappingTable()
    {
        foreach (PadControl control in PadControls.All)
        {
            sourceControlByDestination[(int)control] = (int)control;
        }
    }

    public static PadMappingTable BuildIdentity()
    {
        return new PadMappingTable();
    }

    public static PadMappingTable Build(ControllerConfig controllerConfig, Dictionary<string, string> playerMappings)
    {
        var mappingTable = new PadMappingTable();

        if (controllerConfig?.PlatformLayout == null || controllerConfig.PlatformLayout.Count == 0)
        {
            return mappingTable;
        }

        var explicitDestinations = new HashSet<PadControl>();
        var explicitSources = new HashSet<PadControl>();

        foreach (var platformButton in controllerConfig.PlatformLayout)
        {
            if (!PadControls.TryParseStandardName(platformButton.Value, out PadControl destinationControl))
            {
                continue;
            }

            PadControl sourceControl = destinationControl;

            if (playerMappings != null
                && playerMappings.TryGetValue(platformButton.Key, out string chosenInput)
                && !PadControls.TryParseStandardName(chosenInput, out sourceControl))
            {
                GD.Print($"[InputLayer] mapping '{platformButton.Key}' -> '{chosenInput}' is not a recognised control; falling back to '{platformButton.Value}'.");
                sourceControl = destinationControl;
            }

            mappingTable.sourceControlByDestination[(int)destinationControl] = (int)sourceControl;
            explicitDestinations.Add(destinationControl);
            explicitSources.Add(sourceControl);
        }

        mappingTable.SilenceReroutedSources(explicitDestinations, explicitSources);
        return mappingTable;
    }

    private void SilenceReroutedSources(HashSet<PadControl> explicitDestinations, HashSet<PadControl> explicitSources)
    {
        foreach (PadControl sourceControl in explicitSources)
        {
            if (explicitDestinations.Contains(sourceControl))
            {
                continue;
            }

            sourceControlByDestination[(int)sourceControl] = UnmappedSource;
        }
    }

    public void Apply(PadState physicalState, PadState virtualState)
    {
        virtualState.Clear();

        foreach (PadControl destinationControl in PadControls.All)
        {
            int sourceControl = sourceControlByDestination[(int)destinationControl];

            if (sourceControl == UnmappedSource)
            {
                continue;
            }

            virtualState.Raise(destinationControl, physicalState.Get((PadControl)sourceControl));
        }
    }
}
