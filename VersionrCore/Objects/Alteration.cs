﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Versionr.Objects
{
    public enum AlterationType
    {
        Add,
        Update,
        Move,
        Copy,
        Delete,
        Discard
    }
    public class Alteration
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public long Id { get; set; }
        [SQLite.Indexed]
        public long Owner { get; set; }
        public AlterationType Type { get; set; }
        public long? PriorRecord { get; set; }
        [SQLite.Indexed]
        public long? NewRecord { get; set; }
    }
}
