﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Versionr.Objects;

namespace Versionr
{
    public enum StatusCode
    {
        Unversioned,
        Unchanged,
        Added,
        Modified,
        Missing,
        Deleted,
        Renamed,
        Copied,
        Conflict,
		Ignored,

        Count
    }
    public class Status
    {
        public class StatusEntry
        {
            public StatusCode Code { get; set; }
            public bool Staged { get; set; }
            public Entry FilesystemEntry { get; set; }
            public Record VersionControlRecord { get; set; }
        }
        public List<Objects.Record> VersionControlRecords { get; set; }
        public List<Objects.Record> BaseRecords { get; set; }
        public List<Objects.Alteration> Alterations { get; set; }
        public Objects.Version CurrentVersion { get; set; }
        public Branch Branch { get; set; }
        public List<StatusEntry> Elements { get; set; }
        public List<LocalState.StageOperation> Stage { get; set; }
        public Area Workspace { get; set; }
        public bool HasData
        {
            get
            {
                foreach (var x in Elements)
                {
                    if (x.Staged)
                        return true;
                }
                return false;
            }
        }
        public bool HasModifications(bool requireStaging)
        {
            HashSet<string> addedFiles = new HashSet<string>(Stage.Where(x => x.Type == LocalState.StageOperationType.Add).Select(x => x.Operand1));
            foreach (var x in Elements)
            {
                if (x.Staged == true)
                    return true;
                else if (x.Code == StatusCode.Modified && !requireStaging)
                {
                    return true;
                }
            }
            return false;
        }
        [Flags]
        internal enum StageFlags
        {
            Recorded = 1,
            Removed = 2,
            Renamed = 4,
            Conflicted = 8,
        }
        internal Status(Area workspace, WorkspaceDB db, LocalDB ldb, FileStatus currentSnapshot)
        {
            Workspace = workspace;
            CurrentVersion = db.Version;
            Branch = db.Branch;
            Elements = new List<StatusEntry>();
            var tasks = new List<Task<StatusEntry>>();
            Dictionary<string, Entry> snapshotData = new Dictionary<string, Entry>();
            foreach (var x in currentSnapshot.Entries)
                snapshotData[x.CanonicalName] = x;
            HashSet<Entry> foundEntries = new HashSet<Entry>();
            List<Objects.Record> baserecs;
            List<Objects.Alteration> alterations;
            var records = db.GetRecords(CurrentVersion, out baserecs, out alterations);
            BaseRecords = baserecs;
            Alterations = alterations;
            var stage = ldb.StageOperations;
            Stage = stage;
            VersionControlRecords = records;
            HashSet<string> recordCanonicalNames = new HashSet<string>();
            foreach (var x in records)
                recordCanonicalNames.Add(x.CanonicalName);
            Dictionary<string, StageFlags> stageInformation = new Dictionary<string, StageFlags>();
            foreach (var x in stage)
            {
                if (!x.IsFileOperation)
                    continue;
                StageFlags ops;
                if (!stageInformation.TryGetValue(x.Operand1, out ops))
                    stageInformation[x.Operand1] = ops;
                if (x.Type == LocalState.StageOperationType.Add)
                    ops |= StageFlags.Recorded;
                if (x.Type == LocalState.StageOperationType.Conflict)
                    ops |= StageFlags.Conflicted;
                if (x.Type == LocalState.StageOperationType.Remove)
                    ops |= StageFlags.Removed;
                if (x.Type == LocalState.StageOperationType.Rename)
                    ops |= StageFlags.Renamed;
                stageInformation[x.Operand1] = ops;
            }
            foreach (var x in records)
            {
                tasks.Add(Task.Run<StatusEntry>(() =>
                {
                    StageFlags objectFlags;
                    stageInformation.TryGetValue(x.CanonicalName, out objectFlags);
                    Entry snapshotRecord = null;
                    if (snapshotData.TryGetValue(x.CanonicalName, out snapshotRecord))
                    {
                        lock (foundEntries)
                            foundEntries.Add(snapshotRecord);

                        if (objectFlags.HasFlag(StageFlags.Removed))
                            Printer.PrintWarning("Removed object `{0}` still in filesystem!", x.CanonicalName);

                        if (objectFlags.HasFlag(StageFlags.Renamed))
                            Printer.PrintWarning("Renamed object `{0}` still in filesystem!", x.CanonicalName);

                        if (objectFlags.HasFlag(StageFlags.Conflicted))
                            return new StatusEntry() { Code = StatusCode.Conflict, FilesystemEntry = snapshotRecord, VersionControlRecord = x, Staged = objectFlags.HasFlag(StageFlags.Recorded) };

						if (!snapshotRecord.Ignored && (snapshotRecord.Length != x.Size || ((!snapshotRecord.IsDirectory && (snapshotRecord.ModificationTime != x.ModificationTime)) && snapshotRecord.Hash != x.Fingerprint)))
                            return new StatusEntry() { Code = StatusCode.Modified, FilesystemEntry = snapshotRecord, VersionControlRecord = x, Staged = objectFlags.HasFlag(StageFlags.Recorded) };
                        else
                        {
                            if (objectFlags.HasFlag(StageFlags.Recorded))
                                Printer.PrintWarning("Unchanged object `{0}` still marked as recorded in commit!", x.CanonicalName);
                            return new StatusEntry() { Code = StatusCode.Unchanged, FilesystemEntry = snapshotRecord, VersionControlRecord = x, Staged = objectFlags.HasFlag(StageFlags.Recorded) };
                        }
                    }
                    else
                    {
                        if (objectFlags.HasFlag(StageFlags.Removed))
                            return new StatusEntry() { Code = StatusCode.Deleted, FilesystemEntry = null, VersionControlRecord = x, Staged = true };
                        return new StatusEntry() { Code = StatusCode.Missing, FilesystemEntry = null, VersionControlRecord = x, Staged = false };
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            Elements.AddRange(tasks.Where(x => x != null).Select(x => x.Result));
            foreach (var x in snapshotData)
            {
				if (x.Value.Ignored)
					continue;

                StageFlags objectFlags;
                stageInformation.TryGetValue(x.Value.CanonicalName, out objectFlags);
                if (!foundEntries.Contains(x.Value))
                {
                    foreach (var y in records)
                    {
                        if (y.Size != 0 && x.Value.Length == y.Size && x.Value.Hash == y.Fingerprint)
                        {
                            StageFlags otherFlags;
                            stageInformation.TryGetValue(y.CanonicalName, out otherFlags);
                            if (otherFlags.HasFlag(StageFlags.Removed))
                                Elements.Add(new StatusEntry() { Code = StatusCode.Renamed, FilesystemEntry = x.Value, Staged = objectFlags.HasFlag(StageFlags.Recorded), VersionControlRecord = y });
                            else
                            {
                                if (objectFlags.HasFlag(StageFlags.Recorded))
                                {
                                    Elements.Add(new StatusEntry() { Code = StatusCode.Copied, FilesystemEntry = x.Value, Staged = true, VersionControlRecord = y });
                                }
                                else if (!snapshotData.ContainsKey(y.CanonicalName))
                                {
                                    Elements.Add(new StatusEntry() { Code = StatusCode.Renamed, FilesystemEntry = x.Value, Staged = false, VersionControlRecord = y });
                                }
                                else
                                {
                                    Elements.Add(new StatusEntry() { Code = StatusCode.Copied, FilesystemEntry = x.Value, Staged = false, VersionControlRecord = y });
                                }
                                goto Next;
                            }
                        }
                    }
                    if (objectFlags.HasFlag(StageFlags.Recorded))
                    {
                        Elements.Add(new StatusEntry() { Code = StatusCode.Added, FilesystemEntry = x.Value, Staged = true, VersionControlRecord = null });
                        goto Next;
                    }
                    if (objectFlags.HasFlag(StageFlags.Conflicted))
                    {
                        Elements.Add(new StatusEntry() { Code = StatusCode.Conflict, FilesystemEntry = x.Value, Staged = objectFlags.HasFlag(StageFlags.Recorded), VersionControlRecord = null });
                        goto Next;
                    }
                    Elements.Add(new StatusEntry() { Code = StatusCode.Unversioned, FilesystemEntry = x.Value, Staged = false, VersionControlRecord = null });
                    Next:;
                }
            }
        }
    }
}