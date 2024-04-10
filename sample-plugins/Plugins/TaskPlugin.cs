using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.Graph;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace sample_plugins.Plugins
{
    public sealed class TaskPlugin
    {
        private static readonly IConfigurationRoot _config = ConfigurationProvider.GetConfiguration();
        private static readonly string? _clientId = _config.GetSection("MSGraph:ClientId").Get<string>();
        private static readonly string? _tenantId = _config.GetSection("MSGraph:TenantId").Get<string>();
        private static readonly string? _clientSecret = _config.GetSection("MSGraph:ClientSecret").Get<string>();
        private static readonly string? _taskId = _config.GetSection("MSGraph:TaskId").Get<string>();
        private static readonly string? _userId = _config.GetSection("MSGraph:UserId").Get<string>();

        [KernelFunction, Description("Create a new TODO task")]
        public static async Task<List<TodoTask?>> CreateTaskAsync(string report)
        {
            // Authenticate using ClientCredential flow
            var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);

            var graphClient = new GraphServiceClient(credential);

            var taskList = ExtractTodoListFromEmailReport(report);

            List<TodoTask?> newTasks = new();
            foreach (var task in taskList)
            {
                var createdTask = await graphClient.Users[_userId].Todo
                    .Lists[_taskId].Tasks
                    .PostAsync(task);

                newTasks.Add(createdTask);
            }

            return newTasks ?? throw new InvalidOperationException("Task creation failed.");
        }

        private static List<TodoTask> ExtractTodoListFromEmailReport(string report)
        {
            var todoList = new List<TodoTask>();
            var todoString = report.Split("## Todo List:").Last();
            var todoItems = todoString.Split("- [ ]");
            foreach (var item in todoItems)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    var newTask = new TodoTask
                    {
                        Title = item,
                        Status = Microsoft.Graph.Models.TaskStatus.NotStarted
                    };

                    todoList.Add(newTask);
                }
            }

            return todoList;
        }
    }
}
