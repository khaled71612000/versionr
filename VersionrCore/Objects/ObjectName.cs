﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Versionr.Objects
{
    [ProtoBuf.ProtoContract]
    public class ObjectName
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public long Id { get; set; }
        [ProtoBuf.ProtoMember(1)]
        [SQLite.Indexed]
        public string CanonicalName { get; set; }
    }
}