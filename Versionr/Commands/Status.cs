﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace Versionr.Commands
{
    class StatusVerbOptions : FileBaseCommandVerbOptions
    {
        [Option('l', "nolist", HelpText = "Does not display a listing of file statuses.")]
        public bool NoList { get; set; }
        [Option('s', "summary", HelpText = "Displays a summary at the end of the status block.")]
        public bool Summary { get; set; }
		[Option('a', "all", HelpText = "Includes unchanged files.")]
		public bool All { get; set; }
		[Option('f', "flat", HelpText = "Formats list as flat, instead of partitioned by status.")]
		public bool Flat { get; set; }
        [Option("recordinfo", HelpText = "Shows record information.")]
        public bool RecordInfo { get; set; }
        public override string Usage
        {
            get
            {
                return string.Format("#b#versionr #i#{0}## #q#[options]## [object name specifier]", Verb);
            }
        }

        public override string[] Description
        {
            get
            {
                return new string[]
                {
                    "#q#This command will determine the current status of the Versionr repository tree.", 
                    "",
                    "The operation will involve walking the entire directory tree of the vault and comparing each file against the #b#currently checked out version#q#.",
                    "",
                    "Objects that have operations #b#recorded#q# for the next ##commit#q# are marked with an asterisk (#b#*#q#).",
                    "",
                    "The following status codes are available:",
                    "  #s#Added##: Not part of the vault, but marked for inclusion.",
                    "  #s#Modified##: Object has changed, and the changes are marked for inclusion.",
                    "  #b#Deleted##: Was part of the vault, deleted from the disk and marked for removal.",
                    "  #w#Unversioned##: Object is not part of vault.",
                    "  #w#Missing##: Object is part of the vault, but is not present on the disk.",
                    "  #w#Changed##: Object has changed, but the changes are not marked for inclusion.",
                    "  #w#Renamed##: Object has been matched to a deleted object in the vault.",
                    "  #w#Copied##: Object is not part of the vault but is a copy of an object that is.",
                    "  #e#Conflict##: The file #b#requires intervention## to finish merging. It will #e#obstruct the next commit## until it is resolved.",
					"  #q#Unchanged##: The object has no changes. Only displayed with the #b#--all## option.",
                    "",
                    "To record additional objects for inclusion in the next #b#commit#q#, see the #b#record#q# command.",
                    "",
                    "To reduce computation time, files are #b#not#q# checked for modifications unless their ##timestamp has been changed#q# from the current version#q#.",
                    "",
                    "The `#b#status#q#` command will respect patterns in the #b#.vrmeta#q# directive file.",
                };
            }
        }

        public override string Verb
        {
            get
            {
                return "status";
            }
        }

        public override BaseCommand GetCommand()
        {
            return new Status();
        }
    }
    class Status : FileBaseCommand
    {
        StatusVerbOptions LocalOptions;
        protected override void Start()
        {
			//Printer.WriteLineMessage("Version #b#{0}## on branch \"#b#{1}##\" (rev {2})", Workspace.Version.ID, Workspace.CurrentBranch.Name, Workspace.Version.Revision);
			Printer.WriteLineMessage("Branch #g#{1}## ({2}) #b#{0}##", Workspace.Version.ID, Workspace.CurrentBranch.Name, Workspace.Version.Revision);
        }

        protected override bool RunInternal(Area ws, Versionr.Status status, IList<Versionr.Status.StatusEntry> targets, FileBaseCommandVerbOptions options)
        {
            StatusVerbOptions localOptions = (StatusVerbOptions)options;
            LocalOptions = localOptions;
            if (localOptions.Objects != null && localOptions.Objects.Count > 0)
            {
                if (targets.Count == 0)
                {
                    Printer.PrintError("#x#Error:##\n  Could not find objects matching pattern #b#{0}##", GetPatterns(localOptions.Objects));
                    if (ActiveDirectory.FullName != Workspace.Root.FullName)
                        Printer.PrintMessage("  - Relative to folder \"#b#{0}##\"", Workspace.GetLocalPath(ActiveDirectory.FullName));
                    return false;
                }
            }
            var ss = status;
            if (!string.IsNullOrEmpty(ss.RestrictedPath))
                Printer.WriteLineMessage("  Computing status for path: #b#{0}##", ss.RestrictedPath);

            int[] codeCount = new int[(int)StatusCode.Count];
            if (status.MergeInputs.Count > 0)
                Printer.WriteLineMessage("Workspace has #b#{0}## pending merges.", status.MergeInputs.Count);
            foreach (var x in status.MergeInputs)
            {
                Printer.WriteLineMessage(" #c#{0}#q# from branch \"#b#{1}##\" (rev {2})", x.ID, Workspace.GetBranch(x.Branch).Name, x.Revision);
            }
			if (status.MergeInputs.Count > 0)
				Printer.WriteLineMessage("");
            IEnumerable<Versionr.Status.StatusEntry> operands = targets.Where(x =>
            {
                codeCount[(int)x.Code]++;
                if (x.Code == StatusCode.Excluded)
                    return false;
                if (x.Code == StatusCode.Ignored)
                {
                    if (x.VersionControlRecord != null)
                        return true;
                    return localOptions.Ignored;
                }
                return true;
            });
            if (!localOptions.All)
                operands = operands.Where(x => x.Code != StatusCode.Unchanged);
            string localRestrictedPath = null;
            if (ss.RestrictedPath != null)
                localRestrictedPath = ws.GetLocalCanonicalName(ss.RestrictedPath);


			if (!localOptions.NoList)
			{
				if (localOptions.Flat)
				{
					foreach (var x in operands.OrderBy(x => x.CanonicalName))
					{
						PrintFile(ws, localRestrictedPath, x, true);
					}
				}
				else
				{
					var staged = operands.OrderBy(x => x.CanonicalName).Where(y => y.Staged);
					if (staged.Count() > 0)
					{
						Printer.WriteLineMessage("");
						Printer.WriteLineMessage(" Changes recorded for commit:");
						Printer.WriteLineMessage("  (use \"vsr unrecord <file>...\" to unrecord)");
						Printer.WriteLineMessage("");
						foreach (var x in staged)
						{
							PrintFile(ws, localRestrictedPath, x);
						}
					}

					var nonstaged = operands.OrderBy(x => x.CanonicalName).Where(y => !y.Staged && y.Code != StatusCode.Unversioned);
					if (nonstaged.Count() > 0)
					{
						Printer.WriteLineMessage("");
						Printer.WriteLineMessage(" Changes not staged:");
						Printer.WriteLineMessage("  (use \"vsr record <file>...\" to add files to this record)");
						Printer.WriteLineMessage("  (use \"vsr revert <file>...\" to revert changes from working directory)");
						Printer.WriteLineMessage("");
						foreach (var x in nonstaged)
						{
							PrintFile(ws, localRestrictedPath, x);
						}
					}

					var untracked = operands.OrderBy(x => x.CanonicalName).Where(y => y.Code == StatusCode.Unversioned);
					if (untracked.Count() > 0)
					{
						Printer.WriteLineMessage("");
						Printer.WriteLineMessage(" Untracked files:");
						Printer.WriteLineMessage("  (use \"vsr record <file>...\" to add files to this record)");
						Printer.WriteLineMessage("");
						foreach (var x in untracked)
						{
							PrintFile(ws, localRestrictedPath, x);
						}
					}
				}
			}

			if (localOptions.Summary)
            {
                Printer.WriteLineMessage("\n#b#Summary:##");
                for (int i = 0; i < codeCount.Length; i++)
                    Printer.WriteLineMessage("  {0} #b#{2}## #q#{1}##", codeCount[i], codeCount[i] != 1 ? "Objects" : "Object", ((StatusCode)i).ToString());
                Printer.WriteLineMessage("\n  {0}#q# files in ##{1}#q# diectories ({2} ignored)", ss.Files, ss.Directories, ss.IgnoredObjects);
            }

            foreach (var x in Workspace.Externs)
            {
                bool include = true;
                if (!string.IsNullOrEmpty(ss.RestrictedPath))
                    include = Workspace.PathContains(ss.RestrictedPath, System.IO.Path.Combine(ws.Root.FullName, x.Value.Location));
                else
                    include = Filter(new KeyValuePair<string, object>[] { new KeyValuePair<string, object>(x.Value.Location, new object()) }).FirstOrDefault().Value != null;
                if (include)
                {
                    System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(System.IO.Path.Combine(Workspace.Root.FullName, x.Value.Location));
                    if (directory.Exists)
                    {
                        Printer.WriteLineMessage("\nExternal #c#{0}## ({1}):", x.Key, x.Value.Location);
                        new Status() { DirectExtern = true }.Run(directory, options);
                    }
                    else
                        Printer.WriteLineMessage("\nExternal #c#{0}## ({1}): #e#missing##", x.Key, x.Value.Location);
                }
            }

            var heads = Workspace.GetBranchHeads(Workspace.CurrentBranch);
            if (Workspace.CurrentBranch.Terminus.HasValue)
            {
                Printer.WriteLineMessage("\n #w#Warning:## Current branch \"#b#{0}##\" has been marked as deleted.", Workspace.CurrentBranch.Name);
                if (Workspace.CurrentBranch.Terminus.Value == Workspace.Version.ID)
                    Printer.PrintMessage("You are on the final revision of this branch.");
            }
            else if (heads.Count == 0)
                Printer.WriteLineMessage("\n #w#Warning:## Current branch \"#b#{0}##\" does not have a head!", Workspace.CurrentBranch.Name);
            else if (heads.Count != 1 || heads[0].Version != Workspace.Version.ID)
            {
                if (heads.Count == 1)
                    Printer.WriteLineMessage("\n #w#Warning:## Current version is not the head of branch \"#b#{0}##\"!", Workspace.CurrentBranch.Name);
                else if (heads.Count > 1 && heads.Any(x => x.Version == Workspace.Version.ID))
                {
                    Printer.WriteLineMessage("\n #w#Warning:## Branch \"#b#{0}##\" has multiple heads. Current version is one of #b#{1}## heads!", Workspace.CurrentBranch.Name, heads.Count);
                    int mergeHeadCount = 0;
                    var mergeInputs = Workspace.StagedMergeInputs;
                    foreach (var mi in mergeInputs)
                    {
                        if (heads.Any(h => h.Version == mi))
                            mergeHeadCount++;
                    }
                    if (mergeHeadCount == heads.Count - 1)
                        Printer.WriteLineMessage(" - When the current version is committed, #s#all## heads will be reconciled.");
                    else if (mergeHeadCount != 0)
                        Printer.WriteLineMessage(" - When the current version is committed, #w#{0}# additional heads will be reconciled.", mergeHeadCount);
                }
                else if (heads.Count > 1)
                    Printer.WriteLineMessage("\n #w#Warning:## Branch \"#b#{0}##\" has multiple heads. Current version is #w#not## one of the #b#{1}## heads!", Workspace.CurrentBranch.Name, heads.Count);

                Printer.WriteLineMessage("\n Heads of #b#\"{0}\"##:", Workspace.CurrentBranch.Name);
                foreach (var x in heads)
                {
                    var v = Workspace.GetVersion(x.Version);
                    Printer.WriteLineMessage("   #b#{0}##: {1} by {2}", v.ShortName, v.Timestamp, v.Author);
                }
            }
            return true;
        }

		private void PrintFile(Area ws, string localRestrictedPath, Versionr.Status.StatusEntry x, bool flat = false)
		{
			string name = ws.GetLocalCanonicalName(x.CanonicalName);

			if (FilterOptions.WindowsPaths)
				name = name.Replace('/', '\\');

			if (name != ".vrmeta" && localRestrictedPath != null)
                name = name.Substring(localRestrictedPath.Length);
                
			int index = name.LastIndexOf('/');
			if (index != name.Length - 1)
				name = name.Insert(index + 1, "#b#");
			if (name.Length == 0)
				name = "#q#<parent directory>##";
			if (x.IsSymlink)
				name += " #q# -> " + (x.FilesystemEntry != null ? x.FilesystemEntry.SymlinkTarget : x.VersionControlRecord.Fingerprint);
            string ro = string.Empty;
            if (x.FilesystemEntry != null && x.FilesystemEntry.Attributes.HasFlag(Versionr.Objects.Attributes.ReadOnly))
            {
                if (x.VersionControlRecord != null && !x.VersionControlRecord.Attributes.HasFlag(Versionr.Objects.Attributes.ReadOnly))
                    ro = " #q#(#b#+#q# read only)##";
                else
                    ro = " #q#(read only)##";
            }
            Printer.WriteLineMessage("{1}##{0}{2}", name, GetStatus(x, flat), ro);
			if (x.Code == StatusCode.Renamed || x.Code == StatusCode.Copied)
				Printer.WriteLineMessage("                  #q#<== {0}", x.VersionControlRecord.CanonicalName);
            if (LocalOptions.RecordInfo && !x.IsDirectory)
            {
                if (x.VersionControlRecord != null)
                {
                    if (x.Code == StatusCode.Unchanged)
                        Printer.WriteLineMessage(" Record ID: #c#{0}", x.VersionControlRecord.DataIdentifier);
                    else if (x.Code == StatusCode.Copied || x.Code == StatusCode.Renamed)
                        Printer.WriteLineMessage(" Record ID: #c#{0} (duplicated)", x.VersionControlRecord.DataIdentifier);
                    else
                        Printer.WriteLineMessage(" Record ID: #w#{0} (stale)", x.VersionControlRecord.DataIdentifier);
                }
                else
                {
                    Printer.WriteLineMessage(" Record ID: #w#(new - computed: {0}-{1})##", x.FilesystemEntry.Hash, x.FilesystemEntry.Length);
                }
            }
        }

		private string GetPatterns(IList<string> objects)
        {
            var patterns = objects.Select(x => "`" + x + "`").ToList();
            if (patterns.Count == 1)
                return patterns[0];
            return "[" + string.Join(", ", patterns) + "]";
        }

		const int s_FileLeadingSpaces = 4;

		private string GetStatus(Versionr.Status.StatusEntry x, bool flat = false)
		{
			if (!flat && x.Code == StatusCode.Unversioned)
			{
				return new string(' ', s_FileLeadingSpaces);
			}
			else
			{
				var info = GetStatusText(x);
				string format = (flat) ? "    {0}{1, 12} " : "    {0}{1, -11} ";
                return String.Format(format, "#" + info.Item1 + "#", info.Item2 + ":");
			}
		}

        protected override bool ComputeTargets(FileBaseCommandVerbOptions localOptions)
        {
            return true;
        }

        protected override bool OnNoTargetsAssumeAll
        {
            get
            {
                return true;
            }
        }

        public static Tuple<char, string> GetStatusText(Versionr.Status.StatusEntry x)
        {
            return GetStatusText(x.Code, x.Staged, x.VersionControlRecord != null);
        }

        public static Tuple<char, string> GetStatusText(Versionr.StatusCode code, bool staged, bool isRecorded = false)
        {
            switch (code)
            {
                case StatusCode.Added:
                    return staged ? new Tuple<char, string>('s', "added")
                        : new Tuple<char, string>('e', "error");
                case StatusCode.Conflict:
                    return staged ? new Tuple<char, string>('e', "conflict")
                        : new Tuple<char, string>('e', "conflict");
                case StatusCode.Obstructed:
                    return staged ? new Tuple<char, string>('e', "obstructed")
                        : new Tuple<char, string>('e', "obstructed");
                case StatusCode.Copied:
                    return staged ? new Tuple<char, string>('s', "copied")
                        : new Tuple<char, string>('w', "copied");
                case StatusCode.Deleted:
                    return staged ? new Tuple<char, string>('c', "deleted")
                        : new Tuple<char, string>('w', "missing");
                case StatusCode.Missing:
                    return staged ? new Tuple<char, string>('e', "error")
                        : new Tuple<char, string>('w', "missing");
                case StatusCode.Removed:
                    return staged ? new Tuple<char, string>('q', "removed")
                        : new Tuple<char, string>('w', "ignored");
                case StatusCode.Ignored:
                    return staged ? new Tuple<char, string>('c', "u+ignored")
                        : isRecorded ? new Tuple<char, string>('e', "ignored") : new Tuple<char, string>('q', "ignored");
                case StatusCode.IgnoredAdded:
                    return staged ? new Tuple<char, string>('g', "a+ignored")
                        : new Tuple<char, string>('e', "ignored-remote-add");
                case StatusCode.IgnoredModified:
                    return staged ? new Tuple<char, string>('s', "m+ignored")
                        : new Tuple<char, string>('e', "ignored-remote-merge");
                case StatusCode.Modified:
                    return staged ? new Tuple<char, string>('s', "modified")
                        : new Tuple<char, string>('M', "changed");
                case StatusCode.Renamed:
                    return staged ? new Tuple<char, string>('s', "renamed")
                        : new Tuple<char, string>('w', "renamed");
                case StatusCode.Unversioned:
                    return staged ? new Tuple<char, string>('e', "error")
                        : new Tuple<char, string>('a', "unversioned");
				case StatusCode.Excluded:
					return staged ? new Tuple<char, string>('e', "error")
						: new Tuple<char, string>('q', "ignored");
                case StatusCode.RogueRepository:
                    return staged ? new Tuple<char, string>('M', "rogue-repo")
                        : new Tuple<char, string>('M', "rogue-repo");
				case StatusCode.Unchanged:
					return staged ? new Tuple<char, string>('e', "unchanged")
						: new Tuple<char, string>('q', "unchanged");
				default:
                    throw new Exception();
            }
        }
    }
}
