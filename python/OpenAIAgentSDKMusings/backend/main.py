from fastapi import FastAPI
from pydantic import BaseModel
import os
from openai import OpenAI
from dotenv import load_dotenv
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
import asyncio
from collections import deque

# ✅ Load environment variables
load_dotenv()

# ✅ Define FastAPI app
app = FastAPI()

# ✅ Enable CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Allow requests from any frontend
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ✅ OpenAI Client
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))

# ✅ In-memory chat history (stores last 20 messages)
chat_history = deque(maxlen=20)

# ✅ Define request model
class ChatRequest(BaseModel):
    message: str

# ✅ Streaming response generator (Handles chat history)
async def generate_stream(request: ChatRequest):
    # Store user message in history
    chat_history.append({"role": "user", "content": request.message})

    # Construct messages from history
    messages = list(chat_history)

    # Call OpenAI API with full chat history
    response = client.chat.completions.create(
        model="gpt-4o",
        messages=messages,  # ✅ Use chat history for context
        stream=True
    )

    ai_response = ""  # Store AI response

    for chunk in response:
        if chunk.choices and chunk.choices[0].delta.content:
            token = chunk.choices[0].delta.content
            yield token  # ✅ Stream token-by-token
            ai_response += token  # ✅ Store AI response

    # Store AI response in chat history
    chat_history.append({"role": "assistant", "content": ai_response})

# ✅ Chat endpoint with streaming (POST only)
@app.post("/chat")
async def chat_endpoint(request: ChatRequest):
    return StreamingResponse(generate_stream(request), media_type="text/plain")

# ✅ Clear chat history manually
@app.post("/clear-history")
async def clear_history():
    chat_history.clear()
    return {"message": "Chat history cleared!"}

# ✅ Serve frontend from "frontend" directory (Fixes 404 Errors)
if os.path.exists("frontend") and os.path.exists("frontend/index.html"):
    app.mount("/", StaticFiles(directory="frontend", html=True), name="frontend")
else:
    print("⚠️ WARNING: 'frontend' directory or 'index.html' not found. Static files will not be served.")
