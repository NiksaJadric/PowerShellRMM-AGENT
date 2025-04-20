// Models/DbModels.cs
using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace YourAgent.Models
{
    // Tell Supabase which table this maps to
    [Table("agents")]
    public class Agent : BaseModel
    {
        // PrimaryKey defaults to shouldInsert:false so you won't send "id" in your INSERT
        [PrimaryKey("id", shouldInsert: false)]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("last_seen")]
        public DateTime LastSeen { get; set; }
    }

    [Table("jobs")]
    public class AgentJob : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        [Column("id")]               public Guid Id        { get; set; }
        [Column("agent_id")]         public Guid AgentId   { get; set; }
        [Column("script")]           public string Script  { get; set; }
        [Column("created_at")]       public DateTime CreatedAt { get; set; }
        [Column("status")]           public string Status  { get; set; }
    }

    [Table("job_logs")]
    public class AgentJobLog : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        [Column("id")]               public int Id         { get; set; }
        [Column("job_id")]           public Guid JobId     { get; set; }
        [Column("output")]           public string Output  { get; set; }
        [Column("is_error")]         public bool IsError   { get; set; }
        [Column("logged_at")]        public DateTime LoggedAt { get; set; }
    }
}
