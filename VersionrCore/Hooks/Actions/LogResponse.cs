﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Versionr.Hooks.Actions
{
    public class LogResponse : IHookAction
    {
        public LogResponse(Newtonsoft.Json.Linq.JObject configuration)
        {

        }
        public bool Raise(IHook hook, string filtername)
        {
            Printer.PrintMessage("{2}: Got a hook event: {0}{1} [{3}]", hook.Name, hook.IsServerHook ? " (server)" : " (client)", hook.Timestamp, string.IsNullOrEmpty(filtername) ? "no filter" : filtername);

            if (!string.IsNullOrEmpty(hook.Message))
                Printer.PrintMessage("Message: {0}", hook.Message);

            var branch = hook.Branch;
            if (branch != null)
                Printer.PrintMessage("Associated with branch: {0} ({1})", branch.Name, branch.ID);
            var versionList = hook.AdditionalVersions;
            if (versionList != null)
            {
                foreach (var x in versionList)
                {
                    Printer.PrintMessage("Associated with v {0}: b - {1}", x.ID, x.Branch);
                }
            }
            var modifications = hook.Modifications;
            if (modifications != null)
            {
                foreach (var x in modifications)
                {
                    Printer.PrintMessage("{0} - {1}", x.Key, x.Value);
                }
            }
            return true;
        }
    }
}
