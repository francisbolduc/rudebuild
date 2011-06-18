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

		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInstance, ref Array custom)
		{
			_application = (DTE2)application;
            _addInInstance = (AddIn)addInInstance;

			if (connectMode == ext_ConnectMode.ext_cm_UISetup)
			{
                _commandManager = new CommandManager(_application, _addInInstance);

                try
                {
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("RudeBuild initialization error! " + ex.Message, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Error);
                }
			}
		}

		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
		}

		public void OnAddInsUpdate(ref Array custom)
		{
		}

		public void OnStartupComplete(ref Array custom)
		{
		}

		public void OnBeginShutdown(ref Array custom)
		{
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