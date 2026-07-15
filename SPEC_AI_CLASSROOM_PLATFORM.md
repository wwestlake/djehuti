# AI Classroom Platform — Full Specification

## 1. Vision

Build an **AI-augmented teaching platform** where:
- Teachers design lessons once, deliver them multiple ways (solo AI, co-teach, student-guided)
- Each student has an AI tutor that responds to *both* their input AND teacher directives
- Students chat with each other in real-time while learning together
- Lesson plans are action-driven (not slide-based)—every teaching moment is an interactive possibility
- AI helps teachers prepare lessons, deliver them, and adapt in real-time

## 2. System Overview

### 2.1 Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                    Browser Clients                          │
│  ┌──────────────────┐         ┌──────────────────────────┐  │
│  │  Teacher View    │         │   Student View (N)       │  │
│  │  - Classroom     │         │  - Chat with classmates  │  │
│  │  - Controls      │         │  - AI tutor on screen    │  │
│  │  - Directives    │         │  - Teaching canvas       │  │
│  └──────────────────┘         └──────────────────────────┘  │
└──────────────┬────────────────────────────┬─────────────────┘
               │                            │
        ┌──────▼──────────────────────────────▼──────┐
        │    WebSocket / Real-time Layer              │
        │  - Chat messages (broadcast)                │
        │  - Teacher directives (sideband)            │
        │  - Classroom state sync                     │
        └──────┬─────────────────────────────────────┘
               │
        ┌──────▼─────────────────────────┐
        │    Djehuti API Server           │
        │  - Classroom management         │
        │  - Lesson plan storage          │
        │  - User/auth                    │
        │  - Persistence                  │
        └─────────────────────────────────┘
```

### 2.2 Key Entities

**Classroom**
- Created by teacher
- Contains multiple students
- Has one active lesson plan
- Maintains shared chat history
- Tracks student progress/state

**Lesson Plan**
- Created by teacher (with AI help)
- Contains Topics
- Each Topic has: content, teaching tools, actions, metadata
- Reusable across classrooms
- Versioned

**Topic**
- Smallest teaching unit
- Has content (markdown, terse like current outlines)
- Has actions (buttons, directives, interactions)
- Can be displayed in multiple ways (AI-directed, student-guided, teacher-controlled)

**Action**
- An interactive element within a topic
- Examples:
  - "Show notation (C major scale)"
  - "Highlight the W-W-H pattern"
  - "Ask: What's the interval from C to E?"
  - "Play the staff"
- Has metadata: who can trigger it (AI, student, teacher, all)

**Student Progress**
- Per-student per-classroom
- Topics completed
- Answers to questions
- State of teaching canvas (what's displayed)
- Chat participation

## 3. Teaching Modes

### 3.1 Solo AI Teaching
- **Teacher role**: Creates/configures lesson plan, watches analytics
- **AI role**: Autonomous; receives full lesson plan, decides pacing, uses actions
- **Student role**: Chats with AI, sees what AI directs; can ask to explore (context-dependent)
- **Use case**: Self-paced learning, 24/7 availability

### 3.2 Co-Teaching (Teacher + AI)
- **Teacher role**: Live instructor; can click actions, send directives; AI assists
- **AI role**: Suggests actions, helps answer questions, adapts based on student responses
- **Student role**: Sees teacher + AI; can ask either; receives coordinated instruction
- **Use case**: Live class where teacher is primary, AI amplifies

### 3.3 Teacher-Directed
- **Teacher role**: Controls the lesson; tells AI what to show/do
- **AI role**: Executes directives; can add context/explanation
- **Student role**: Receives instruction from AI (directed by teacher); participates in chat
- **Use case**: Teacher wants full control; AI is presentation tool

### 3.4 Student-Guided
- **Teacher role**: Configures which actions students can trigger; monitors progress
- **AI role**: Responds to student exploration; guides with questions
- **Student role**: Clicks actions to explore; drives own pacing
- **Use case**: Exploration-based learning, inquiry

## 4. Real-Time Architecture

### 4.1 Message Types

**Chat Messages** (broadcast to all in classroom)
```
{
  type: "chat",
  sender: "student_id" | "teacher_id",
  text: "string",
  timestamp: ISO8601,
  classroom_id: UUID
}
```

**Teacher Directive** (sideband, unicast to specific student)
```
{
  type: "directive",
  from: "teacher_id",
  to: "student_id",
  action: "show_notation" | "highlight" | "ask_question" | ...,
  payload: { ... },  // action-specific data
  timestamp: ISO8601
}
```

**Student Response** (to AI or question)
```
{
  type: "response",
  student_id: UUID,
  to: "ai_tutor" | "question_id",
  content: "string",
  timestamp: ISO8601
}
```

**Classroom State Sync** (periodic or on change)
```
{
  type: "state",
  classroom_id: UUID,
  current_topic: UUID,
  active_students: [{ id, name, status }],
  teaching_canvas_state: { ... }
}
```

### 4.2 Connection Model

- **Server**: Maintains WebSocket connections per student + teacher
- **Teacher**: One connection per classroom they're teaching
- **Student**: One connection per classroom they're in
- **Message routing**:
  - Chat: broadcast to all in classroom
  - Directives: unicast to specific student
  - State: broadcast to all in classroom
  - Responses: logged, available to AI/teacher

## 5. Lesson Plan Structure

### 5.1 Current Format (Kept As-Is)
Topics are terse, conceptually clean outlines:
```
Topic: "The Piano Keyboard: Half Steps and Whole Steps"
Content: "[markdown explanation]"
Kind: "piano" | "notation" | "fretboard" | "markdown"
NotationJson: "[JSON for musical staff]" (optional)
```

### 5.2 Enhanced with Actions

```
{
  id: UUID,
  title: "The Piano Keyboard: Half Steps and Whole Steps",
  content: "[existing markdown]",
  kind: "piano",
  actions: [
    {
      id: "action_show_keyboard",
      label: "Show the keyboard",
      type: "show_tool",
      tool: "piano",
      triggeredBy: ["teacher", "student", "ai"],  // who can invoke
      description: "Display piano keyboard for interaction"
    },
    {
      id: "action_highlight_pattern",
      label: "Highlight the 2-3-2 pattern",
      type: "highlight",
      target: "piano_keys",
      pattern: [2, 3, 2],
      triggeredBy: ["teacher", "ai"]
    },
    {
      id: "action_ask_question",
      label: "How many half steps from C to E?",
      type: "question",
      question: "How many half steps from C4 to E4?",
      expectedAnswer: "4",
      triggeredBy: ["ai"]  // AI asks this
    }
  ]
}
```

### 5.3 AI-Generated Material

- **Companion content**: AI generates deeper explanations, practice problems, real-world examples
- **Not in lesson plan**: Stored separately, retrieved on-demand
- **Triggered by**: Student asks for more depth, AI detects knowledge gap
- **Example**: Student sees "Major scales" → AI can generate 5 major scales with patterns highlighted, practice exercises, historical context

## 6. AI Integration

### 6.1 What the AI Receives

**Initial Context**
```
System Prompt: "You are a music theory tutor in an interactive classroom..."
Classroom Context: {
  teacher: {...},
  students: [...],
  lesson_plan: {...},
  current_topic: {...},
  student_history: [messages, responses, progress]
}
```

**Per-Message Input**
```
{
  student_message: "What's a major scale?",
  current_topic: {...},
  student_progress: { topics_completed: [...] },
  available_actions: [list of actions AI can invoke],
  classroom_state: { active_students: [...] }
}
```

### 6.2 What the AI Can Do

1. **Respond to student**: Answer questions, explain concepts
2. **Invoke actions**: "Show the keyboard" → sends action directive
3. **Ask questions**: Assess understanding
4. **Generate material**: Request AI-generated practice, examples
5. **Suggest next steps**: "Ready for intervals?" (respects lesson structure)
6. **Adapt**: Adjust depth based on student responses

### 6.3 AI Constraints

- **Must stay in context**: Lesson plan is the source of truth
- **Can't skip topics**: Can suggest, but teacher/student decides pacing
- **Can't modify lesson plan**: Only suggest improvements for next iteration
- **Respectful of modes**: In teacher-directed mode, AI executes; in AI-led, AI decides

## 7. Data Model (Conceptual)

### Users
```
users {
  id UUID,
  email,
  role: "teacher" | "student" | "admin",
  display_name
}
```

### Classrooms
```
classrooms {
  id UUID,
  teacher_id UUID (FK users),
  lesson_plan_id UUID (FK lesson_plans),
  name,
  status: "preparing" | "live" | "archived",
  created_at,
  settings: {
    mode: "solo_ai" | "co_teach" | "teacher_directed" | "student_guided",
    max_students: int,
    recording_enabled: bool
  }
}

classroom_members {
  id UUID,
  classroom_id UUID (FK),
  user_id UUID (FK users),
  role: "student" | "observer",
  joined_at,
  progress: { topics_completed, last_topic }
}
```

### Lesson Plans
```
lesson_plans {
  id UUID,
  author_id UUID (FK users),
  title,
  description,
  version,
  topics: [Topic objects],
  created_at,
  updated_at,
  published: bool
}

Topic {
  id UUID,
  title,
  content (markdown),
  kind: "notation" | "piano" | "fretboard" | "markdown",
  notationJson (optional),
  position (order in plan),
  actions: [Action objects],
  metadata: { estimated_duration, difficulty }
}

Action {
  id UUID,
  label,
  type: "show_tool" | "highlight" | "question" | "play" | "custom",
  triggeredBy: ["teacher", "student", "ai"],
  payload: {...}
}
```

### Classroom Messages & State
```
classroom_messages {
  id UUID,
  classroom_id UUID,
  sender_id UUID,
  type: "chat" | "directive" | "response",
  content,
  created_at
}

classroom_state {
  id UUID,
  classroom_id UUID,
  current_topic_id UUID,
  teaching_canvas: {
    displayed_tool: "notation" | "piano" | "fretboard" | null,
    displayed_data: {...}
  },
  updated_at
}
```

## 8. API Endpoints

### Classroom Management
```
POST   /api/classrooms              - Create classroom
GET    /api/classrooms/{id}         - Get classroom details
POST   /api/classrooms/{id}/invite  - Invite student
GET    /api/classrooms/{id}/members - List members
POST   /api/classrooms/{id}/start   - Begin lesson
POST   /api/classrooms/{id}/end     - End lesson
```

### Real-Time (WebSocket)
```
WS /api/classroom/{id}/connect      - Student/teacher joins
  - Send: { type: "chat" | "directive" | ... }
  - Receive: broadcast messages, directives, state updates
```

### Lesson Plans
```
POST   /api/lesson-plans            - Create plan
GET    /api/lesson-plans/{id}       - Get plan
PUT    /api/lesson-plans/{id}       - Update plan
POST   /api/lesson-plans/{id}/topics - Add topic
PUT    /api/lesson-plans/{id}/topics/{tid} - Update topic
POST   /api/lesson-plans/{id}/publish - Publish/finalize
```

### AI-Generated Material
```
POST   /api/lesson-plans/{id}/generate-companion - Request AI-generated depth
GET    /api/companion-material/{id} - Retrieve generated material
```

## 9. UI Flows

### 9.1 Teacher Classroom View
```
┌─────────────────────────────────────────────────────┐
│ Classroom: "Music Theory 101"                       │
├──────────────┬──────────────────────────────────────┤
│ Controls     │  Lesson Progress                     │
│ - Topic list │  - Current: Piano Keyboard           │
│ - Mode sel.  │  - Students: 5 active               │
│ - Directives │  - Next: Intervals                   │
│              │                                      │
│              │  ┌──────────────────────────────────┐│
│              │  │ Student 1: On topic, following   ││
│              │  │ Student 2: Stuck on question     ││
│              │  │ Student 3: Ahead, exploring      ││
│              │  └──────────────────────────────────┘│
│              │                                      │
│              │  Chat Area:                          │
│              │  [S1] How do I find middle C?        │
│              │  [AI] It's the... [Show notation]    │
│              │  [Teacher] Great Q, everyone try it  │
└──────────────┴──────────────────────────────────────┘
```

### 9.2 Student Classroom View
```
┌──────────────────────────────────┬────────────────┐
│  AI Tutor (Main)                 │  Class Chat    │
│                                  │                │
│  AI: "Look at the keyboard..."   │  You: Hi!      │
│  [Show keyboard button]           │  S2: Hi back   │
│                                  │  AI: answer    │
│  You: How many half steps?        │                │
│                                  │                │
│  AI: [directives from teacher]    │                │
│  "Everyone find C on your        │                │
│   keyboard"                      │                │
│                                  │                │
│  Teaching Canvas:                │                │
│  [Piano keyboard showing]         │                │
│                                  │                │
├──────────────────────────────────┴────────────────┤
│ Input: [Type message to AI or classmates]         │
└──────────────────────────────────────────────────┘
```

## 10. Implementation Phases

### Phase 1: Foundation (Weeks 1-2)
- [ ] Classroom model & database
- [ ] WebSocket real-time infrastructure
- [ ] Basic classroom view (teacher creates, students join)
- [ ] Chat synchronization
- [ ] Teacher directive sideband

### Phase 2: AI Integration (Weeks 2-3)
- [ ] Lesson plan → AI context window
- [ ] AI can invoke actions (show notation, highlight, etc.)
- [ ] AI responds to student input
- [ ] AI-student message history
- [ ] Solo AI mode functional

### Phase 3: UI & Experience (Weeks 3-4)
- [ ] Teacher classroom dashboard
- [ ] Student classroom view
- [ ] Teaching canvas rendering (AI-directed tools)
- [ ] Action buttons (student-guided mode)
- [ ] Progress tracking UI

### Phase 4: Polish & Modes (Week 4)
- [ ] Co-teaching mode
- [ ] Teacher-directed mode
- [ ] Student-guided mode
- [ ] Analytics (teacher sees student progress)
- [ ] Mobile responsiveness

### Phase 5: AI Content Generation (Week 5+)
- [ ] AI generates companion material
- [ ] Practice problem generation
- [ ] Example generation
- [ ] On-demand deepening

## 11. Key Design Decisions

1. **Lesson plan as blueprint**: One plan works in multiple teaching modes
2. **Actions as primitives**: Every teaching moment is an action; who triggers it is configurable
3. **AI as flexible role**: Can be solo teacher, assistant, executor
4. **Real-time first**: WebSocket for low-latency directives & chat
5. **Student isolation + connection**: Each student's AI is independent, but shared classroom keeps them together
6. **Keep terse content**: Outlines stay as-is; depth comes from AI generation, not lesson plan bloat

## 12. Success Metrics

- A teacher can create a lesson plan once, teach it in 4 different modes
- A student experiences AI as their tutor, receives directives from teacher, chats with peers—seamlessly
- Teacher can see which students are struggling and redirect in real-time
- Lesson plans are reusable, version-able, shareable
- AI generates material on-demand without cluttering the plan

---

**Next steps**: Review this spec, iterate, then redesign Lesson.razor, create Classroom components, and wire up real-time.
