// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using MongoDB.Bson;
using Newtonsoft.Json;
using Plugins.DictionaryPlugin;
using RepoUtils;

public class Input
{
    public Dictionary<string, Property> Properties { get; set; }
}

public class Property
{
    public string type { get; set; }
    public string description { get; set; }
}

public class Output
{
    public string type { get; set; }
    public string description { get; set; }
}

public class Helper
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<Input> Inputs { get; set; }
    public Output Outputs { get; set; }
}

// This example shows how to use the Handlebars sequential planner.
public static class Example997_HandlebarsPlanner_Autoplugin_CodeInterpreter
{
    private static int s_sampleIndex;

    private const string CourseraPluginName = "CourseraPlugin";

    /// <summary>
    /// Show how to create a plan with Handlebars and execute it.
    /// </summary>
    public static async Task RunAsync()
    {
        s_sampleIndex = 1;

        // Plugin with Complex Types as inputs and outputs
        //await RunLocalDictionaryWithComplexTypesSampleAsync(shouldPrintPrompt: true);

        // Plugin with primitive types as inputs and outputs
        await DynamicCodeInterpreterPluginAsync();
        //await RunDictionaryWithBasicTypesSampleAsync();
        //await RunPoetrySampleAsync();
        //await RunBookSampleAsync();

        // OpenAPI plugin
        //await RunCourseraSampleAsync(true);
    }

    private static void WriteSampleHeadingToConsole(string name)
    {
        Console.WriteLine($"======== [Handlebars Planner] Sample {s_sampleIndex++} - Create and Execute {name} Plan ========");
    }

    private static async Task RunSampleAsync(string goal, bool shouldPrintPrompt = false, KernelPlugin? kp = null)
    {
        //string apiKey = TestConfiguration.AzureOpenAI.ApiKey;
        //string chatDeploymentName = TestConfiguration.AzureOpenAI.ChatDeploymentName;
        //string chatModelId = TestConfiguration.AzureOpenAI.ChatModelId;
        //string endpoint = TestConfiguration.AzureOpenAI.Endpoint;

        //if (apiKey == null || chatDeploymentName == null || chatModelId == null || endpoint == null)
        //{
        //    Console.WriteLine("Azure endpoint, apiKey, deploymentName, or modelId not found. Skipping example.");
        //    return;
        //}

        //var kernel = Kernel.CreateBuilder()
        //    .AddAzureOpenAIChatCompletion(
        //        deploymentName: chatDeploymentName,
        //        endpoint: endpoint,
        //        serviceId: "AzureOpenAIChat",
        //        apiKey: apiKey,
        //        modelId: chatModelId)
        //    .Build();

        string openAIModelId = TestConfiguration.OpenAI.ChatModelId;
        string openAIApiKey = TestConfiguration.OpenAI.ApiKey;

        var kernel = Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(
            modelId: openAIModelId,
            apiKey: openAIApiKey)
        .Build();

        if (kp != null)
        {
            kernel.Plugins.Add(kp);
        }

        //if (pluginDirectoryNames[0] == StringParamsDictionaryPlugin.PluginName)
        //{
        //    kernel.ImportPluginFromType<StringParamsDictionaryPlugin>(StringParamsDictionaryPlugin.PluginName);
        //}
        //else if (pluginDirectoryNames[0] == ComplexParamsDictionaryPlugin.PluginName)
        //{
        //    kernel.ImportPluginFromType<ComplexParamsDictionaryPlugin>(ComplexParamsDictionaryPlugin.PluginName);
        //}
        //else if (pluginDirectoryNames[0] == CourseraPluginName)
        //{
        //    await kernel.ImportPluginFromOpenApiAsync(
        //        CourseraPluginName,
        //        new Uri("https://www.coursera.org/api/rest/v1/search/openapi.yaml")
        //    );
        //}
        //else
        //{
        //    string folder = RepoFiles.SamplePluginsPath();

        //    foreach (var pluginDirectoryName in pluginDirectoryNames)
        //    {
        //        kernel.ImportPluginFromPromptDirectory(Path.Combine(folder, pluginDirectoryName));
        //    }
        //}

        // Use gpt-4 or newer models if you want to test with loops. 
        // Older models like gpt-35-turbo are less recommended. They do handle loops but are more prone to syntax errors.
        var allowLoopsInPlan = true; //chatDeploymentName.Contains("gpt-4", StringComparison.OrdinalIgnoreCase);
        var planner = new HandlebarsPlanner(
            new HandlebarsPlannerOptions()
            {
                // Change this if you want to test with loops regardless of model selection.
                AllowLoops = allowLoopsInPlan
            });

        Console.WriteLine($"Goal: {goal}");

        // Create the plan
        var plan = await planner.CreatePlanAsync(kernel, goal);

        // Print the prompt template
        if (shouldPrintPrompt && plan.Prompt is not null)
        {
            Console.WriteLine($"\nPrompt template:\n{plan.Prompt}");
        }

        Console.WriteLine($"\nOriginal plan:\n{plan}");

        // Execute the plan
        var result = await plan.InvokeAsync(kernel);
        Console.WriteLine($"\nResult:\n{result}\n");
    }

    private static string kfunctiontest()
    {
        Console.WriteLine("kfunctiontest called");
        Console.ReadLine();
        return "";
    }

    private static async Task DynamicCodeInterpreterPluginAsync(bool shouldPrintPrompt = false)
    {


        WriteSampleHeadingToConsole("DynamicCodeInterpreterPlugin");

        try
        {
            KernelPlugin kp = null;
            bool needMorePlugins = true;
            while (needMorePlugins)
            {
                // Load additional plugins to enable planner but not enough for the given goal.
                try
                {
                    needMorePlugins = false;
                    await RunSampleAsync("Tell me in the 1000 first decimals of Pi, what is the ratio of even vs odd numbers?", shouldPrintPrompt, kp);
                }
                catch (Exception e)
                {
                    needMorePlugins = true;
                    Console.WriteLine(e.Message);
                    Match match = Regex.Match(e.Message, @"\[\s*\{.*\}\s*\]", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        var helpers = match.Value;
                        List<Helper> required_helpers = JsonConvert.DeserializeObject<List<Helper>>(helpers);
                        kp = KernelPluginFactory.CreateFromFunctions("DynamicPlugins", new List<KernelFunction>() {
                            KernelFunctionFactory.CreateFromMethod(kfunctiontest)
                        });
                        foreach (Helper h in required_helpers)
                        {
                            Console.WriteLine($"Plugin {h.Name} is required.");

                        }
                    }
                }
            }
        }
        catch (KernelException ex) when (
            ex.Message.Contains(nameof(HandlebarsPlannerErrorCodes.InsufficientFunctionsForGoal), StringComparison.CurrentCultureIgnoreCase)
            || ex.Message.Contains(nameof(HandlebarsPlannerErrorCodes.HallucinatedHelpers), StringComparison.CurrentCultureIgnoreCase))
        {
            /*
                Unable to create plan for goal with available functions.
                Goal: Email me a list of meetings I have scheduled today.
                Available Functions: SummarizePlugin-Notegen, SummarizePlugin-Summarize, SummarizePlugin-MakeAbstractReadable, SummarizePlugin-Topics
                Planner output:
                I'm sorry, but it seems that the provided helpers do not include any helper to fetch or filter meetings scheduled for today. 
                Therefore, I cannot create a Handlebars template to achieve the specified goal with the available helpers. 
                Additional helpers may be required.
            */
            Console.WriteLine($"\n{ex.Message}\n");
        }
    }
}
