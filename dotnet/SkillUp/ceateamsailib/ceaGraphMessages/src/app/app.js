const { MemoryStorage, MessageFactory } = require("botbuilder");
const path = require("path");
const config = require("../config");

// See https://aka.ms/teams-ai-library to learn more about the Teams AI library.
const { Application, ActionPlanner, OpenAIModel, PromptManager } = require("@microsoft/teams-ai");

// Create AI components
const model = new OpenAIModel({
  apiKey: config.openAIKey,
  defaultModel: "gpt-3.5-turbo",

  useSystemMessages: true,
  logRequests: true,
});
const prompts = new PromptManager({
  promptsFolder: path.join(__dirname, "../prompts"),
});
const planner = new ActionPlanner({
  model,
  prompts,
  defaultPrompt: "chat",
});

// Define storage and application
const storage = new MemoryStorage();
const app = new Application({
  storage,
  ai: {
    planner,
  },
});

app.conversationUpdate("membersAdded", async (turnContext) => {
  const welcomeText = "How can I help you today?";
  for (const member of turnContext.activity.membersAdded) {
    if (member.id !== turnContext.activity.recipient.id) {
      await turnContext.sendActivity(MessageFactory.text(welcomeText));
    }
  }
});

const { generateAdaptiveCard, addAuthConfig } = require("./utility.js");
const yaml = require("js-yaml");
const { OpenAPIClientAxios } = require("openapi-client-axios");
const fs = require("fs-extra");
// Define a prompt function for getting the current status of the lights
planner.prompts.addFunction("getAction", async (context, memory) => {
  const specFilePath = path.join(__dirname, "../prompts/chat/actions.json");
  const specFileContent = fs.readFileSync(specFilePath);
  return specFileContent.toString();
});
const specPath = path.join(__dirname, "../../appPackage/apiSpecificationFile/openapi.json");
const specContent = yaml.load(fs.readFileSync(specPath, "utf8"));
const api = new OpenAPIClientAxios({ definition: specContent });
api.init();


app.ai.action("readEmails", async (context, state, parameter) => {
  const client = await api.getClient();
  addAuthConfig(client);
  const path = client.paths["/me/messages"];
  if (path && path.get) {
    const result = await path.get(parameter.path, parameter.body, {
      params: parameter.query,
    });
    const cardName = "readEmails".replace(/[^a-zA-Z0-9]/g, "_");
    const card = generateAdaptiveCard("../adaptiveCards/" + cardName + ".json", result);
    await context.sendActivity({ attachments: [card] });
  } else {
    await context.sendActivity("no result");
  }
  return "result";
});
  

module.exports = app;
