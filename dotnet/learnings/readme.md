# What is Semantic Kernel

**Semantic Kernel** is an open-source SDK that empowers developers to build their own custom artificially intelligent (AI) agents. By combining large language models (LLMs) with native code, developers can create AI agents that understand and respond to natural language prompts to complete a variety of tasks.

## What's an AI agent?

An AI agent is a program that can achieve predetermined goals. AI agents are powered by large language models (LLMs) that are trained on massive amounts of data. An AI agent can fulfill a wide variety of tasks with minimal human intervention. AI agents can help you:

- Write code.
- Compose emails.
- Summarize meetings.
- Provide recommendations.

And the list goes on!

## What's Semantic Kernel?

Semantic Kernel integrates LLMs like **OpenAI**, **Azure OpenAI**, and **Hugging Face** with conventional programming languages like **C#**, **Python**, and **Java**. Developers can create "plugins" to interface with the LLMs and perform various tasks. 

The Semantic Kernel SDK also provides built-in plugins that can quickly enhance an application. Developers can easily utilize LLMs in their own applications without having to learn the intricacies of the model's API.

## Key Components of the Semantic Kernel SDK

### AI Orchestration Layer

At the core of the Semantic Kernel stack is an **AI orchestration layer** that allows for the seamless integration of AI models and plugins. This layer is responsible for combining these components to craft innovative user interactions.

### Connectors

The Semantic Kernel SDK offers a set of **connectors** that enable developers to integrate LLMs into their existing applications. These connectors serve as the bridge between the application code and the AI models.

### Plugins

The Semantic Kernel SDK operates on **plugins**, which essentially serve as the "body" of the AI app. Plugins consist of prompts that you want the AI model to respond to and functions that can complete specialized tasks. Developers can use built-in plugins or create their own to customize functionality.

### For Employees, Copilots and custom engine agents Can:

- Automate repetitive tasks.
- Provide relevant insights.
- Boost efficiency and productivity.
- And more!

### For Customers, Copilots and custom engine agents Can:

- Streamline processes.
- Offer personalized recommendations.
- Provide 24/7 availability.
- And more!

When using the **Semantic Kernel SDK**, plugins act as the building blocks of your artificially intelligent (AI) application. Plugins define the tasks the kernel should complete. The kernel interfaces with a large language model (LLM) and runs the plugins you provide. 

Your plugins can include native code and natural language prompts, allowing you to use generative AI to perform a wide variety of tasks in your applications.

## What is a Plugin?

In the **Semantic Kernel SDK**, a plugin is a class that contains functions that can be run by the kernel. A plugin function can be made from a semantic prompt or native function code. To use a plugin, you add it to the kernel and then call the desired function using the `InvokeAsync` method. The kernel accesses the plugin, locates and runs the function, then returns the result.

Let's take a look at some predefined plugins.

## Built-in Plugins

The Semantic Kernel SDK offers an extra package with predefined plugins for common tasks. These are available in the **Plugins.Core** package, which you can install with NuGet:

```bash
dotnet add package Microsoft.SemanticKernel.Plugins.Core --version 1.2.0-alpha
```

## Defining Roles

You can also provide instructions to the LLM to assume a role when generating a response, and provide example requests and responses. In Semantic Kernel, a special syntax is used to define message roles.

To define a message role, you can wrap the message in a `<message>` tag with the role name as an attribute. The supported roles are:

- `user`
- `system`
- `assistant`
- `bot`

