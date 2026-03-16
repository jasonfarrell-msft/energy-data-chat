import { useState, useRef, useEffect } from 'react'
import { IoSend } from 'react-icons/io5'
import { FaSun, FaUser, FaChartBar } from 'react-icons/fa'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import {
  ResponsiveContainer, LineChart, Line, BarChart, Bar,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend
} from 'recharts'

const API_URL = 'https://aca-energy-chat-api-eus2-mx01.gentlewater-91f388b4.eastus2.azurecontainerapps.io/chat'

const CHART_COLORS = ['#0079c1', '#f0512c', '#448359', '#142c41', '#365453']

/**
 * Extracts chartable data from the first markdown table in a string.
 * Returns { headers, rows, chartData, numericKeys } or null.
 */
function extractChartData(text) {
  if (!text) return null

  // Match a markdown pipe table (header row, separator row, data rows)
  const tableRegex = /\|(.+)\|\r?\n\|[-| :]+\|\r?\n((?:\|.+\|\r?\n?)+)/
  const match = text.match(tableRegex)
  if (!match) return null

  const headers = match[1].split('|').map(h => h.trim()).filter(Boolean)
  const dataRows = match[2].trim().split('\n').map(row =>
    row.split('|').map(c => c.trim()).filter(Boolean)
  )

  if (dataRows.length < 2 || headers.length < 2) return null

  // Identify which columns are numeric
  const numericCols = headers.reduce((acc, header, idx) => {
    const numCount = dataRows.filter(row => {
      const val = row[idx]?.replace(/[,%$]/g, '')
      return val && !isNaN(parseFloat(val))
    }).length
    if (numCount >= dataRows.length * 0.5) acc.push(idx)
    return acc
  }, [])

  if (numericCols.length === 0) return null

  // First non-numeric column is the label axis
  const labelIdx = headers.findIndex((_, i) => !numericCols.includes(i))
  if (labelIdx === -1) return null

  const chartData = dataRows.map(row => {
    const point = { name: row[labelIdx] || '' }
    numericCols.forEach(ci => {
      point[headers[ci]] = parseFloat(row[ci]?.replace(/[,%$]/g, '') || '0')
    })
    return point
  })

  const numericKeys = numericCols.map(i => headers[i])

  return { headers, rows: dataRows, chartData, numericKeys, labelKey: headers[labelIdx] }
}

function ChatChart({ data }) {
  const { chartData, numericKeys } = data
  const useBar = chartData.length <= 6

  const ChartComponent = useBar ? BarChart : LineChart
  const SeriesComponent = useBar ? Bar : Line

  return (
    <div className="chart-container">
      <ResponsiveContainer width="100%" height={280}>
        <ChartComponent data={chartData} margin={{ top: 5, right: 20, bottom: 5, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#e0e0e0" />
          <XAxis dataKey="name" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} />
          <Tooltip />
          {numericKeys.length > 1 && <Legend />}
          {numericKeys.map((key, i) => (
            <SeriesComponent
              key={key}
              dataKey={key}
              {...(useBar
                ? { fill: CHART_COLORS[i % CHART_COLORS.length] }
                : { stroke: CHART_COLORS[i % CHART_COLORS.length], strokeWidth: 2, dot: { r: 3 } }
              )}
            />
          ))}
        </ChartComponent>
      </ResponsiveContainer>
    </div>
  )
}

function BotMessage({ content }) {
  const chartData = extractChartData(content)
  const [showChart, setShowChart] = useState(false)

  return (
    <>
      {!showChart && (
        <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
      )}
      {showChart && chartData && <ChatChart data={chartData} />}
      {chartData && (
        <button
          className="chart-toggle-btn"
          onClick={() => setShowChart(prev => !prev)}
        >
          <FaChartBar />
          {showChart ? 'View as Table' : 'View as Chart'}
        </button>
      )}
    </>
  )
}

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
                  <div className="message-bubble">
                    {msg.role === 'bot'
                      ? <BotMessage content={msg.content} />
                      : msg.content}
                  </div>
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
