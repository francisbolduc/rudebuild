using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Windows;

namespace RudeBuildAddIn
{
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{
        private DTE2 _application;
        private AddIn _addInInstance;
        private CommandManager _commandManager;
        private OutputPane _outputPane;
        private Builder _builder;

        private void RegisterCommands()
        {
            _commandManager.RegisterCommand("BuildSolution", "&Build Solution", "RudeBuild: Build Solution", "3", new BuildSolutionCommand(_builder, BuildCommandBase.Mode.Build));
            _commandManager.RegisterCommand("RebuildSolution", "&Rebuild Solution", "RudeBuild: Rebuild Solution", null, new BuildSolutionCommand(_builder, BuildCommandBase.Mode.Rebuild));
            _commandManager.RegisterCommand("CleanSolution", "&Clean Solution", "RudeBuild: Clean Solution", null, new BuildSolutionCommand(_builder, BuildCommandBase.Mode.Clean));
            _commandManager.RegisterCommand("BuildProject", "B&uild Project", "RudeBuild: Build Project", "2", new BuildProjectCommand(_builder, BuildCommandBase.Mode.Build));
            _commandManager.RegisterCommand("RebuildProject", "R&ebuild Project", "RudeBuild: Rebuild Project", null, new BuildProjectCommand(_builder, BuildCommandBase.Mode.Rebuild));
            _commandManager.RegisterCommand("CleanProject", "Clea&n Project", "RudeBuild: Clean Project", null, new BuildProjectCommand(_builder, BuildCommandBase.Mode.Clean));
            _commandManager.RegisterCommand("StopBuild", "&Stop Build", "RudeBuild: Stop Build", "5", new StopBuildCommand(_builder));
            _commandManager.RegisterCommand("About", "&About", "About RudeBuild", null, new AboutCommand());
        }

        private void AddToolbarToUI()
        {
            CommandBar commandBar = _commandManager.AddCommandBar("RudeBuild", MsoBarPosition.msoBarTop);
            int insertIndex = 1;
            _commandManager.AddCommandToCommandBar(commandBar, "BuildProject", insertIndex++, style: MsoButtonStyle.msoButtonIcon);
            _commandManager.AddCommandToCommandBar(commandBar, "BuildSolution", insertIndex++, style: MsoButtonStyle.msoButtonIcon);
            _commandManager.AddCommandToCommandBar(commandBar, "StopBuild", insertIndex++, beginGroup:true, style: MsoButtonStyle.msoButtonIcon);
        }

        private void AddMenuToUI()
        {
            CommandBar commandBar = _commandManager.AddPopupCommandBar("MenuBar", "RudeBuild", "R&udeBuild", GetMainMenuBarInsertIndex());
            int insertIndex = 1;
            _commandManager.AddCommandToCommandBar(commandBar, "BuildSolution", insertIndex++);
            _commandManager.AddCommandToCommandBar(commandBar, "RebuildSolution", insertIndex++);
            _commandManager.AddCommandToCommandBar(commandBar, "CleanSolution", insertIndex++);
            _commandManager.AddCommandToCommandBar(commandBar, "BuildProject", insertIndex++, beginGroup: true);
            _commandManager.AddCommandToCommandBar(commandBar, "RebuildProject", insertIndex++);
            _commandManager.AddCommandToCommandBar(commandBar, "CleanProject", insertIndex++);

            _commandManager.AddCommandToCommandBar(commandBar, "StopBuild", insertIndex++, beginGroup: true);

            /*
            _commandManager.AddCommandToCommandBar(commandBar, "GlobalSettings", insertIndex++, beginGroup: true);
            _commandManager.AddCommandToCommandBar(commandBar, "SolutionSettings", insertIndex++);
            */

            _commandManager.AddCommandToCommandBar(commandBar, "About", insertIndex++, beginGroup: true);
        }

        private void AddCommandsToUI()
        {
            AddToolbarToUI();
            AddMenuToUI();
        }

        private void RemoveCommandsFromUI()
        {
            CommandBar toolCommandBar = _commandManager.FindCommandBar("RudeBuild");
            if (null != toolCommandBar)
                toolCommandBar.Delete();
            CommandBar menuCommandBar = _commandManager.FindPopupCommandBar("MenuBar", "RudeBuild");
            if (null != menuCommandBar)
                menuCommandBar.Delete();
        }

        private int GetMainMenuBarInsertIndex()
        {
            CommandBarControl commandBarControl = _commandManager.FindCommandBarControlByCaption("MenuBar", "IncrediBuild");
            if (null != commandBarControl)
                return commandBarControl.Index;

            commandBarControl = _commandManager.FindCommandBarControlByCaption("MenuBar", "Build");
            if (null != commandBarControl)
                return commandBarControl.Index + 1;

            return 5;
        }

        private int GetPopupMenuBarInsertIndex(string popupMenuBarName)
        {
            CommandBarControl commandBarControl = _commandManager.FindCommandBarControlByCaption(popupMenuBarName, "IncrediBuild");
            if (null != commandBarControl)
                return commandBarControl.Index;

            commandBarControl = _commandManager.FindCommandBarControlByCaption(popupMenuBarName, "Project Dependencies...");
            if (null != commandBarControl)
                return commandBarControl.Index;

            return 5;
        }

		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInstance, ref Array custom)
		{
            //MessageBox.Show("Start debugging!", "RudeBuild", MessageBoxButton.OK);
            
            _application = (DTE2)application;
            _addInInstance = (AddIn)addInInstance;

            try
            {
                _commandManager = new CommandManager(_application, _addInInstance);
                _outputPane = new OutputPane(_application, "RudeBuild");
                _builder = new Builder(_application, _outputPane);

                RegisterCommands();
                AddCommandsToUI();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("RudeBuild initialization error! " + ex.Message, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Error);
            }
		}

		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
            if (null != _commandManager)
                RemoveCommandsFromUI();
        }

		public void OnAddInsUpdate(ref Array custom)
		{
		}

		public void OnStartupComplete(ref Array custom)
		{
		}

		public void OnBeginShutdown(ref Array custom)
		{
            if (null != _commandManager)
                RemoveCommandsFromUI();
		}

        private static string GetShortCommandName(string longCommandName)
        {
            int dotIndex = longCommandName.LastIndexOf('.');
            if (dotIndex != -1)
                return longCommandName.Substring(dotIndex + 1);
            else
                return longCommandName;
        }

		public void QueryStatus(string longCommandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
		{
			if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
			{
                string commandName = GetShortCommandName(longCommandName);
                if (_commandManager.IsCommandEnabled(commandName))
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                else
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported;
			}
		}

        public void Exec(string longCommandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
		{
			handled = false;
			if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
                string commandName = GetShortCommandName(longCommandName);
                _commandManager.ExecuteCommand(commandName);
                handled = true;
			}
		}
	}
}