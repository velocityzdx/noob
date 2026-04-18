const bTopic = "velocityzdx_trollerlink_74f9d2x1";
const CMD_TOPIC = `${bTopic}/cmd`;
const RAW_TOPIC = `${bTopic}/raw`;

let client;
let isStreaming = false;

const statusIndicator = document.getElementById("connectionStatus");
const liveScreen = document.getElementById("liveScreen");
const screenOverlay = document.getElementById("screenOverlay");
const btnToggleScreen = document.getElementById("btnToggleScreen");
const terminalOutput = document.getElementById("terminalOutput");

// Init
function init() {
    statusIndicator.className = "status-indicator connecting";
    client = mqtt.connect("wss://broker.hivemq.com:8884/mqtt");

    client.on("connect", () => {
        statusIndicator.className = "status-indicator connected";
        client.subscribe(RAW_TOPIC);
        appendTerminal("Connected to TrollerLink Broker securely.");
        sendCommand("ping", "");
    });

    client.on("message", (topic, message) => {
        if (topic === RAW_TOPIC) {
            try {
                const data = JSON.parse(message.toString());
                handleResponse(data);
            } catch(e) { console.error("Parse error", e); }
        }
    });

    client.on("error", (err) => {
        statusIndicator.className = "status-indicator disconnected";
        appendTerminal(`Connection error: ${err.message}`);
    });
}

function sendCommand(action, payload = "") {
    if(!client || !client.connected) return;
    const msg = JSON.stringify({ action, payload });
    client.publish(CMD_TOPIC, msg, { qos: 0 });
}

function handleResponse(data) {
    if(data.type === "frame") {
        liveScreen.src = "data:image/jpeg;base64," + data.data;
        screenOverlay.style.display = "none";
    }
    else if(data.type === "terminal") {
        appendTerminal(data.data);
    }
    else if(data.type === "tasks") {
        renderTasks(JSON.parse(data.data));
    }
}

// UI Handlers
btnToggleScreen.onclick = () => {
    isStreaming = !isStreaming;
    if(isStreaming) {
        btnToggleScreen.innerHTML = '<i class="fa-solid fa-stop"></i> Stop';
        btnToggleScreen.classList.replace("primary-btn", "danger-btn");
        sendCommand("stream_start");
    } else {
        btnToggleScreen.innerHTML = '<i class="fa-solid fa-play"></i> Start';
        btnToggleScreen.classList.replace("danger-btn", "primary-btn");
        sendCommand("stream_stop");
    }
};

// Terminal
function appendTerminal(text) {
    terminalOutput.textContent += text + "\n";
    terminalOutput.scrollTop = terminalOutput.scrollHeight;
}

document.getElementById("btnSendCmd").onclick = () => {
    const input = document.getElementById("cmdInput");
    if(input.value) {
        sendCommand("run_cmd", input.value);
        appendTerminal(`> ${input.value}`);
        input.value = "";
    }
};

// Tasks
document.getElementById("btnRefreshTasks").onclick = () => sendCommand("get_tasks");
document.getElementById("btnStartTask").onclick = () => {
    const input = document.getElementById("newTaskInput");
    if(input.value) sendCommand("start_task", input.value);
};

function renderTasks(tasks) {
    const list = document.getElementById("taskList");
    list.innerHTML = "";
    tasks.forEach(t => {
        const li = document.createElement("li");
        li.className = "task-item";
        li.innerHTML = `
            <div class="task-info">
                <span class="task-name">${t.Name}</span>
                <span class="task-pid">PID: ${t.Id} | Mem: ${t.Mem}MB</span>
            </div>
            <button class="btn danger-btn" onclick="sendCommand('kill_task', '${t.Id}')"><i class="fa-solid fa-xmark"></i></button>
        `;
        list.appendChild(li);
    });
}

// Keyboard
document.querySelectorAll(".vkey").forEach(btn => {
    btn.onclick = () => sendCommand("keystroke", btn.dataset.key);
});
document.getElementById("btnSendText").onclick = () => {
    const input = document.getElementById("textInput");
    if(input.value) sendCommand("text", input.value);
};

// Tabs
document.querySelectorAll(".tab-btn").forEach(btn => {
    btn.onclick = () => {
        document.querySelectorAll(".tab-btn").forEach(b => b.classList.remove("active"));
        document.querySelectorAll(".tab-content").forEach(c => c.style.display = "none");
        btn.classList.add("active");
        document.getElementById(btn.dataset.tab).style.display = "flex";
    };
});

init();
