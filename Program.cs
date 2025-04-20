// Program.cs

// 1. Bring in the namespaces you'll need
using System;                                // Basic types (Environment, Console)
using System.Linq;                           // LINQ extensions (Select, etc.)
using System.Management.Automation;         // Host PowerShell Core in‑process
using System.Threading.Tasks;               // Task, async/await
using DotNetEnv;                            // for environmental variables
using Supabase;                              // Supabase client SDK
using YourAgent.Models;                      // Your POCO classes: Agent, Job, JobLog
using Supabase.Postgrest.Exceptions;

Env.Load();  

// 2. Configure your Supabase endpoint and key
//    Replace these with the URL & key from your Supabase project settings
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")!;
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")!;

// 3. Create and initialize the Supabase client
//    This sets up the HTTP connection, authentication, schema mapping, etc.
Console.WriteLine("Initializing Supabase client...");
var client = new Supabase.Client(supabaseUrl, supabaseKey);
await client.InitializeAsync();
Console.WriteLine("Supabase client initialized.");

// 4. Register this machine as an "agent" and capture the INSERT response
Agent me;

try
{
    Console.WriteLine("Registering agent…");
    var resp = await client
      .From<Agent>()
      .Insert(new Agent { Name = Environment.MachineName });

    me = resp.Model
         ?? throw new InvalidOperationException("Agent insert returned no rows.");
}
catch (PostgrestException pex)
{
    Console.Error.WriteLine(
      $"POST /agents failed → HTTP {(int?)pex.Response?.StatusCode}: “{pex.Content}”");
    throw;
}

Console.WriteLine($"Agent registered: {me.Id}");



// 5. Enter the main polling loop
//    This will run forever (or until you kill the process), checking every 10s
while (true)
{
    // 5a. Fetch any pending jobs for THIS agent
    //     We're looking for rows in `jobs` where AgentId matches and Status == "pending"
    Console.WriteLine($"\n[{DateTime.Now:O}] Checking for pending jobs for agent {me.Id}...");

    var pending = await client
      .From<AgentJob>()
      .Where(j => j.AgentId == me.Id && j.Status == "pending")
      .Get();

    Console.WriteLine($"Fetched {pending.Models.Count} pending job(s).");

    // 5b. Process each job one by one
    foreach (var job in pending.Models)
    {
        Console.WriteLine($"---\nProcessing job {job.Id} (created at {job.CreatedAt}):");

        // Create a PowerShell runspace
        using var ps = PowerShell.Create();

        // Load the script text from the job record
        ps.AddScript(job.Script);

        // Execute it synchronously; collects output and errors
        var results = ps.Invoke();

        // 5c. Turn the PSObjects into a single string output
        var output = string.Join(
            Environment.NewLine,
            results.Select(r => r.ToString())
        );

        // 5d. Insert a log entry into your `job_logs` table
        Console.WriteLine($"Logging output for job {job.Id} (errors: {ps.HadErrors})...");
        await client
          .From<AgentJobLog>()
          .Insert(new AgentJobLog {
            JobId   = job.Id,
            Output  = output,
            IsError = ps.HadErrors,            // true if any errors occurred
          });
        Console.WriteLine("Log entry inserted.");

        // 5e. Mark the job as done so you don’t re‑run it
        Console.WriteLine($"Marking job {job.Id} as done...");
        job.Status = "done";
        await client
          .From<AgentJob>()
          .Where(j => j.Id == job.Id)
          .Update(job);
        Console.WriteLine("Job marked done.");
    }

    // 6. Wait before polling again (10 seconds here; adjust as you like)
    Console.WriteLine("Sleeping for 10 seconds...");
    await Task.Delay(TimeSpan.FromSeconds(10));
}
