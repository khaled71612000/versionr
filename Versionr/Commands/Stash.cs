﻿using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Versionr.Commands
{
    class StashVerbOptions : VerbOptionBase
    {
        public override string Usage
        {
            get
            {
                return string.Format("Usage: versionr {0} stash_name", Verb);
            }
        }

        public override string[] Description
        {
            get
            {
                return new string[]
                {
                    "Stash all currently staged changes to a named stash file and reverts them to a pristine state."
                };
            }
        }

        public override string Verb
        {
            get
            {
                return "stash";
            }
        }
        [Option('r', "revert", DefaultValue = true, HelpText = "Reverts file changes once they are stashed away.")]
        public bool Revert { get; set; }

        [ValueOption(0)]
        public string Name { get; set; }
    }
    class Stash : BaseCommand
    {
        public bool Run(System.IO.DirectoryInfo workingDirectory, object options)
        {
            StashVerbOptions localOptions = options as StashVerbOptions;
            Printer.EnableDiagnostics = localOptions.Verbose;
            Area ws = Area.Load(workingDirectory);
            if (ws == null)
                return false;
            ws.Stash(localOptions.Name, localOptions.Revert, Unrecord.UnrecordFeedback);
            return true;
        }
    }
}