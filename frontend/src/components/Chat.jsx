import { useState, useRef, useEffect } from 'react'
import { IoSend } from 'react-icons/io5'
import { FaSun, FaUser } from 'react-icons/fa'

const API_URL = 'https://aca-energy-chat-api-eus2-mx01.gentlewater-91f388b4.eastus2.azurecontainerapps.io/chat'

function Chat() {
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [conversationId, setConversationId] = useState(null)
  const messagesEndRef = useRef(null)
  const textareaRef = useRef(null)

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, loading])

  const adjustTextareaHeight = () => {
    const ta = textareaRef.current
    if (ta) {
      ta.style.height = 'auto'
      ta.style.height = Math.min(ta.scrollHeight, 120) + 'px'
    }
  }

  const handleSend = async () => {
    const trimmed = input.trim()
    if (!trimmed || loading) return

    const userMessage = { role: 'user', content: trimmed }
    setMessages(prev => [...prev, userMessage])
    setInput('')
    setLoading(true)

    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto'
    }

    try {
      const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ chatMessage: trimmed, conversationId }),
      })

      if (!res.ok) throw new Error(`HTTP ${res.status}`)

      const data = await res.json()
      setConversationId(data.conversationId)
      setMessages(prev => [...prev, { role: 'bot', content: data.response }])
    } catch {
      setMessages(prev => [
        ...prev,
        { role: 'bot', content: 'Sorry, something went wrong. Please try again.' },
      ])
    } finally {
      setLoading(false)
    }
  }

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <div className="chat-wrapper">
      {/* Header */}
      <div className="chat-header">
        <FaSun className="bot-icon" />
        <div>
          <h1>FusionSun</h1>
          <span className="subtitle">PSEG Energy Chat Assistant</span>
        </div>
      </div>

      {/* Messages */}
      <div className="chat-messages">
        {messages.length === 0 && !loading ? (
          <div className="welcome-screen">
            <FaSun className="welcome-icon" />
            <h2>Welcome to FusionSun</h2>
            <p>Ask me anything about your energy needs.</p>
          </div>
        ) : (
          <div className="chat-messages-inner">
            {messages.map((msg, i) => (
              <div key={i} className={`message-row ${msg.role}`}>
                <div className={`message-avatar ${msg.role}`}>
                  {msg.role === 'bot' ? <FaSun /> : <FaUser />}
                </div>
                <div className="message-content">
                  <div className="message-sender">
                    {msg.role === 'bot' ? 'FusionSun' : 'You'}
                  </div>
                  <div className="message-bubble">{msg.content}</div>
                </div>
              </div>
            ))}

            {loading && (
              <div className="message-row bot">
                <div className="message-avatar bot">
                  <FaSun />
                </div>
                <div className="message-content">
                  <div className="message-sender">FusionSun</div>
                  <div className="message-bubble">
                    <div className="typing-indicator">
                      <span></span>
                      <span></span>
                      <span></span>
                    </div>
                  </div>
                </div>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Input */}
      <div className="chat-input-area">
        <div className="chat-input-inner">
          <textarea
            ref={textareaRef}
            rows={1}
            placeholder="Send a message..."
            value={input}
            onChange={(e) => {
              setInput(e.target.value)
              adjustTextareaHeight()
            }}
            onKeyDown={handleKeyDown}
            disabled={loading}
          />
          <button
            className="send-btn"
            onClick={handleSend}
            disabled={loading || !input.trim()}
            aria-label="Send message"
          >
            <IoSend />
          </button>
        </div>
      </div>
    </div>
  )
}

export default Chat
