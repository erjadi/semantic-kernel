// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Experimental.Agents;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.Plugins.Core;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Showcase complex Open AI Agent collaboration using semantic kernel.
/// </summary>
public static class Example998_AutoAgent
{
    /// <summary>
    /// Specific model is required that supports agents and function calling.
    /// Currently this is limited to Open AI hosted services.
    /// </summary>
    //private const string OpenAIFunctionEnabledModel = "gpt-4-1106-preview";
    private const string OpenAIFunctionEnabledModel = "gpt-3.5-turbo-16k";

    // Track agents for clean-up
    private static readonly List<IAgent> s_agents = new();

    /// <summary>
    /// Show how to combine and coordinate multiple agents.
    /// </summary>
    public static async Task RunAsync()
    {
        if (TestConfiguration.OpenAI.ApiKey == null)
        {
            Console.WriteLine("OpenAI apiKey not found. Skipping example.");
            return;
        }

        // NOTE: Either of these examples produce a conversation
        // whose duration may vary depending on the collaboration dynamics.
        // It is sometimes possible that agreement is never achieved.

        // Explicit collaboration
        await RunCollaborationAsync();

        // Coordinate collaboration as plugin agents (equivalent to previous case - shared thread)
        //await RunAsPluginsAsync();
    }

    /// <summary>
    /// Show how two agents are able to collaborate as agents on a single thread.
    /// </summary>
    private static async Task RunCollaborationAsync()
    {
        Console.WriteLine("======== Run:Collaboration ========");
        IAgentThread? thread = null;
        try
        {
            // Create copy-writer agent to generate ideas
            //var copyWriter = await CreateCopyWriterAsync();
            // Create art-director agent to review ideas, provide feedback and final approval
            //var artDirector = await CreateArtDirectorAsync();

            // Create collaboration thread to which both agents add messages.
            //thread = await copyWriter.NewThreadAsync();

            // Add the user message

            var assignment = @"

            Solve the puzzle that is described at https://projecteuler.net/problem=52


            ";
            //var assignment = "Write a python script that sorts a random array of integers in several ways: Bubble, Selection, Insertion and Cycle.";
            //var assignment = "Write a short children's story that is technically accurate, and explains containerization and orchestration, and appeals to 5 to 7 year old Japanese children.";
            //var assignment = "Create a 5 page operations manual for a Russian Yasen-M or Severodvinsk class submarine, written for 5 to 7 year old Dutch children";

            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();

            List<IAgent> agents = await CreateAgentTeamAsync(assignment);

            stopwatch.Stop();
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopwatch.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

            Console.WriteLine($"Elapsed Time {elapsedTime}");


            stopwatch.Reset();
            var PM = await CreateProjectManagerAsync(agents, assignment); // Create team of agents
            agents.Insert(0,PM); // Make project manager the first agent in the list

            //agents.Add(await CreateProjectCollectorAsync()); // Add Project Collector?

            thread = await PM.NewThreadAsync();
            var messageUser = await thread.AddUserMessageAsync(assignment);

            DisplayMessage(messageUser);

            bool isComplete = false;

            // First round all agents speak once, after that let the Project Manager decide.

            foreach (var agent in agents)
            {

                stopwatch.Start();

                // Initiate copy-writer input
                var agentMessages = await thread.InvokeAsync(agent).ToArrayAsync();
                DisplayMessages(agentMessages, agent);

                stopwatch.Stop();
                // Get the elapsed time as a TimeSpan value.
                ts = stopwatch.Elapsed;

                // Format and display the TimeSpan value.
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);

                Console.WriteLine($"Elapsed Time {elapsedTime}");


                stopwatch.Reset();

                if (agent.Name.Equals("Project Manager"))
                {
                    while (agentMessages[0].Content.Contains("NEXT SPEAKER: functions."))
                    {
                        agentMessages = await thread.InvokeAsync(agent).ToArrayAsync();
                        DisplayMessages(agentMessages, agent);
                    }
                }
            }

            do
            {
                stopwatch.Start();

                var PMMessages = await thread.InvokeAsync(agents[0]).ToArrayAsync(); // PM Speaks
                DisplayMessages(PMMessages, agents[0]);

                stopwatch.Stop();
                // Get the elapsed time as a TimeSpan value.
                ts = stopwatch.Elapsed;

                // Format and display the TimeSpan value.
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);

                Console.WriteLine($"Elapsed Time {elapsedTime}");

                stopwatch.Reset();
                // Find out who should be next
                string pattern = @"NEXT SPEAKER: (\w+)";
                Match match = Regex.Match(PMMessages[0].Content, pattern, RegexOptions.Multiline);

                string nextAgent = "";

                if (PMMessages[0].Content.Contains("FINAL ANSWER", StringComparison.OrdinalIgnoreCase))
                {
                    isComplete = true;
                }

                if (match.Success)
                {
                    nextAgent = match.Groups[1].Value;
                }

                if (nextAgent.Length > 0)
                    foreach (var agent in agents)
                    {
                        if (agent.Name.StartsWith(nextAgent))
                        {
                            stopwatch.Start();

                            // Initiate member input
                            var agentMessages = await thread.InvokeAsync(agent).ToArrayAsync();
                            DisplayMessages(agentMessages, agent);
                            ts = stopwatch.Elapsed;

                            // Format and display the TimeSpan value.
                            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                                ts.Hours, ts.Minutes, ts.Seconds,
                                ts.Milliseconds / 10);

                            Console.WriteLine($"Elapsed Time {elapsedTime}");

                            stopwatch.Reset();
                        }
                    }
                // Initiate copy-writer input
                // Evaluate if goal is met.            }
            } while (!isComplete);
        }
        finally
       {
            // Clean-up (storage costs $)
            await Task.WhenAll(s_agents.Select(a => a.DeleteAsync()));
        }
    }
    private class TeamMember
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Instructions { get; set; }
        public string Description { get; set; }
    }

    private static async Task<List<IAgent>> CreateAgentTeamAsync(string prompt)
    {
        //Console.WriteLine("======== Chat with prompts ========");

        /* Load 3 files:
         * - 28-system-prompt.txt: the system prompt, used to initialize the chat session.
         * - 28-user-context.txt:  the user context, e.g. a piece of a document the user selected and is asking to process.
         * - 28-user-prompt.txt:   the user prompt, just for demo purpose showing that one can leverage the same approach also to augment user messages.
         */

        var systemPromptTemplate = @"
            You are a seasoned project manager that has a lot of experience putting together and managing cross-functional project teams.
            You have a broad understanding of different topics and technologies, and are able to quickly understand what kind of skills are needed to complete a project.
            You break down assignments from the stakeholder into different domains and assign them to team members. Keep in mind that the final result will only be textual or actions. We do not need team members that focus on visual or graphical aspects.
            For this project, you will put together a team of experts between 2 and 5 people that will be able to carry out the assignment.
            Considering the assignment below, list out each team member profile with the following information: name, role, instructions and a description of skills/expertise and duties. The instructions are written in the form of a prompt that the team member will use to complete the assignment.
            The description is single line that describes what this role delivers. We do not need a project manager role, you are the project manager.
            Output in JSON, here is an example:

            [
                {
                    ""name"": ""John"",
                    ""role"": ""Copywriter"",
                    ""instructions"": ""You are a copywriter with ten years of experience and are known for brevity and a dry humor. You're laser focused on the goal at hand. Don't waste time with chit chat. The goal is to refine and decide on the single best copy as an expert in the field.  Consider suggestions when refining an idea."",
                    ""description"": ""John is a strong-headed but fair copywriter that has a strong background in commercial communications. He has worked on B2C products such as cars, electronics and alcoholic beverages.""
                },
                {
                    ""name"": ""Jane"",
                    ""role"": ""Art Director"",
                    ""instructions"": ""You are an art director who has opinions about copywriting born of a love for David Ogilvy. The goal is to determine is the given copy is acceptable to print, even if it isn't perfect.  If not, provide insight on how to refine suggested copy without example.  Always respond to the most recent message by evaluating and providing critique without example.  Always repeat the copy at the beginning.  If copy is acceptable and meets your criteria, say: PRINT IT."",
                    ""description"": ""Jane has grown up in Japan and her esthetic and artistic sensibilities have been shaped by both present and past artists from this country. She is a big fan of Hokusai and his ukiyo-e works.""
                }
            ]

            This is the assignment that the team needs to fulfill:
            {{ $userMessage }}
        ";
        var userPromptTemplate = prompt;

        Kernel kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey, serviceId: "chat")
            .Build();

        // As an example, we import the time plugin, which is used in system prompt to read the current date.
        // We could also use a variable, this is just to show that the prompt can invoke functions.

        // Adding required arguments referenced by the prompt templates.
        var arguments = new KernelArguments
        {
            // This is the user message, store it in the variable used by 28-user-prompt.txt
            ["userMessage"] = prompt
        };

        // Instantiate the prompt template factory, which we will use to turn prompt templates
        // into strings, that we will store into a Chat history object, which is then sent
        // to the Chat Model.
        var promptTemplateFactory = new KernelPromptTemplateFactory();

        // Render the system prompt. This string is used to configure the chat.
        // This contains the context, ie a piece of a wikipedia page selected by the user.
        string systemMessage = await promptTemplateFactory.Create(new PromptTemplateConfig(systemPromptTemplate)).RenderAsync(kernel, arguments);
        //Console.WriteLine($"------------------------------------\n{systemMessage}");

        // Render the user prompt. This string is the query sent by the user
        // This contains the user request, ie "extract locations as a bullet point list"
        string userMessage = await promptTemplateFactory.Create(new PromptTemplateConfig(userPromptTemplate)).RenderAsync(kernel, arguments);
        //Console.WriteLine($"------------------------------------\n{userMessage}");

        // Client used to request answers
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        // The full chat history. Depending on your scenario, you can pass the full chat if useful,
        // or create a new one every time, assuming that the "system message" contains all the
        // information needed.
        var chatHistory = new ChatHistory(systemMessage);

        // Add the user query to the chat history
        chatHistory.AddUserMessage(userMessage);

        // Finally, get the response from AI
        var answer = await chatCompletion.GetChatMessageContentAsync(chatHistory);
        //Console.WriteLine($"------------------------------------\n{answer}");

        string startTag = "```json";
        string endTag = "```";
        string input = answer.Content;

        int startIndex = input.IndexOf(startTag) + startTag.Length;
        int endIndex = input.IndexOf(endTag, startIndex);
        string extractedJSON = input.Substring(startIndex, endIndex - startIndex);
        List<TeamMember> jsonagents = JsonConvert.DeserializeObject<List<TeamMember>>(extractedJSON);

        List<IAgent> agents = new List<IAgent>();
        Console.WriteLine("======== Creating Agents ========");

        string InstructionSuffix = @"
            Only speak from the perspective of your role. You do not need to explain your role and responsibilities, the project manager has already done that.
            Drive towards a result. Don't assume any duties of the other team members, just focus on your own role, for example:

            If you are the technical writer, and someone else is the translator, you would only write in English and leave the translation to the translator.
            If you are the technical expert, and someone else is the child psychologist, you only focus on technical accuracy and leave it to the child psychologist to make it appealing to children.

            Do not speak on behalf of other members.
            Do not try to finalize the result yourself on your own, you rely on your team for that.
            Listen especially to any instructions addressed to you by the project manager.
            If you need any more information regarding the assignment, ask the project manager.
            You can execute Python code in a sandbox. If you execute any code, you must include the code in your message.
            You can access the internet and retrieve websites
            Other members will address you by your name, and you are the only one with this name: 
        ";

        foreach (var a in jsonagents)
        {
            var tempAgent = Track(
                    await new AgentBuilder()
                        .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                        .WithInstructions(a.Instructions + InstructionSuffix + a.Name)
                        .WithName(a.Name + " - " + a.Role)
                        .WithDescription(a.Description)
                        .WithPlugin(KernelPluginFactory.CreateFromType<HttpPlugin>())
                       .BuildAsync());
            agents.Add( tempAgent
                );
            Console.WriteLine(tempAgent.Name);
        }

        return agents;
    }

    /// <summary>
    /// Show how agents can collaborate as agents using the plug-in model.
    /// </summary>
    /// <remarks>
    /// While this may achieve an equivalent result to <see cref="RunCollaborationAsync"/>,
    /// it is not using shared thread state for agent interaction.
    /// </remarks>
    private static async Task RunAsPluginsAsync()
    {
        Console.WriteLine("======== Run:AsPlugins ========");
        try
        {
            // Create copy-writer agent to generate ideas
            var copyWriter = await CreateCopyWriterAsync();
            // Create art-director agent to review ideas, provide feedback and final approval
            var artDirector = await CreateArtDirectorAsync();

            // Create coordinator agent to oversee collaboration
            var coordinator =
                Track(
                    await new AgentBuilder()
                        .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                        .WithInstructions("Reply the provided concept and have the copy-writer generate an marketing idea (copy).  Then have the art-director reply to the copy-writer with a review of the copy.  Always include the source copy in any message.  Always include the art-director comments when interacting with the copy-writer.  Coordinate the repeated replies between the copy-writer and art-director until the art-director approves the copy.")
                        .WithPlugin(copyWriter.AsPlugin())
                        .WithPlugin(artDirector.AsPlugin())
                        .BuildAsync());

            // Invoke as a plugin function
            var response = await coordinator.AsPlugin().InvokeAsync("concept: maps made out of egg cartons.");

            // Display final result
            Console.WriteLine(response);
        }
        finally
        {
            // Clean-up (storage costs $)
            await Task.WhenAll(s_agents.Select(a => a.DeleteAsync()));
        }
    }

    private async static Task<IAgent> CreateProjectManagerAsync(List<IAgent> agents, string assignment)
    {
        string Instructions = $@"
            You are an experienced project manager that only cares about finding a result that satisfies all requirements.
            You listen to each team member's input and collect all information. You have access to the web and are able to retrieve and read texts.

            This is the assignment from the stakeholders: '{assignment}'

            If the assignment contains a link, use your function to retrieve the text from the link and add it to the assignment.
            Take all of the text and data from the link that might be relevant and repeat it verbatim in your message to the team.
            DO NOT omit any of the examples that might also be a part of the link.
            At the start, address each team member individually and explain how they can contribute to finishing the assignment.

            Do not contribute or provide your own input, you rely on your team for that.

            In subsequent rounds, you will then ask each team member to provide their input.You do not try to deliver the result yourself, you rely on your team for that.
            After the first time, when you speak ALWAYS ask a specific team member for their input, describe the type of input you want from them and end your message with :
            ""NEXT SPEAKER:<speaker name>""
            Make sure you drive your team towards a clear result, be crisp and specific in what you ask of them.
            If the result has been achieved write : FINAL ANSWER, followed by the verbatim, unedited final result. These are the members in your team :\n
        ";
        foreach (var a in agents)
        {
            Instructions += a.Name + " - " + a.Description + "\n";
        }

        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions(Instructions)
                    .WithName("Project Manager")
                    .WithDescription("Project Manager")
                    .WithPlugin(KernelPluginFactory.CreateFromType<HttpPlugin>())
                    .BuildAsync());
    }

    private async static Task<IAgent> CreateProjectCollectorAsync()
    {
        string Instructions = @"
            
            Your role in projects is to take all the input that has been given by other members and put it together into a result that best answers the question.
            Do not add any information yourself, you rely on your team for that.  Just give the most complete and correct answer to the original question, based on the inputs given.
            Do not write any accompanying text or sentences, just provide the result.
            ";


        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions(Instructions)
                    .WithName("Eric - Project Collector")
                    .WithDescription("Project Collector")
                    .BuildAsync());
    }

    private async static Task<IAgent> CreateCopyWriterAsync(IAgent? agent = null)
    {
        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions("You are a copywriter with ten years of experience and are known for brevity and a dry humor. You're laser focused on the goal at hand. Don't waste time with chit chat. The goal is to refine and decide on the single best copy as an expert in the field.  Consider suggestions when refining an idea.")
                    .WithName("Copywriter")
                    .WithDescription("Copywriter")
                    .WithPlugin(agent?.AsPlugin())
                    .BuildAsync());
    }

    private async static Task<IAgent> CreateArtDirectorAsync()
    {
        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions("You are an art director who has opinions about copywriting born of a love for David Ogilvy. The goal is to determine is the given copy is acceptable to print, even if it isn't perfect.  If not, provide insight on how to refine suggested copy without example.  Always respond to the most recent message by evaluating and providing critique without example.  Always repeat the copy at the beginning.  If copy is acceptable and meets your criteria, say: PRINT IT.")
                    .WithName("Art Director")
                    .WithDescription("Art Director")
                    .BuildAsync());
    }

    private async static Task<IAgent> CreateDynamicAgentAsync(string Instructions, string Name, string Description,IAgent? agent = null)
    {
        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions(Instructions)
                    .WithName(Name)
                    .WithDescription(Description)
                    .WithPlugin(agent?.AsPlugin())
                    .BuildAsync());
    }

    private static void DisplayMessages(IEnumerable<IChatMessage> messages, IAgent? agent = null)
    {
        foreach (var message in messages)
        {
            DisplayMessage(message, agent);
        }
    }

    private static void DisplayMessage(IChatMessage message, IAgent? agent = null)
    {
        Console.WriteLine($"[{message.Id}]");
        if (agent != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"# {message.Role} :");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"({agent.Name})");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($" {message.Content}");
        }
        else
        {
            Console.WriteLine($"# {message.Role}: {message.Content}");
        }
    }

    private static IAgent Track(IAgent agent)
    {
        s_agents.Add(agent);

        return agent;
    }
}
