#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class ControllerIconPlugin : EditorPlugin
{
	private ControllerIconEditorInspector inspectorPlugin;
	public static EditorInterface EditorInterface 
	{ 
		get
		{
			return EditorInterface.Singleton;
		}
	}

	public override void _EnablePlugin()
	{
		AddAutoloadSingleton("ControllerIcons", "res://addons/controller_icons/ControllerIcons.cs");
	}

	public override void _DisablePlugin()
	{
		RemoveAutoloadSingleton("ControllerIcons"); 
	}

	public override void _EnterTree()
	{
		inspectorPlugin = new();

		AddInspectorPlugin(inspectorPlugin);
	}

	public override void _ExitTree()
	{
		RemoveInspectorPlugin(inspectorPlugin);
	}
}
#endif