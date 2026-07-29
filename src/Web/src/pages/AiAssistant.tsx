import { useState, useRef, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Brain, Send, Sparkles, BarChart3, Monitor, Bell, Shield } from 'lucide-react'

interface Message {
  role: 'user' | 'assistant'
  content: string
}

const suggestions = (t: (key: string) => string) => [
  { icon: Monitor, label: t('aiAssistant.showMeComputersOffline'), query: 'Which computers are currently offline?' },
  { icon: Bell, label: t('aiAssistant.latestCriticalAlerts'), query: 'Show me the latest critical alerts' },
  { icon: BarChart3, label: t('aiAssistant.dashboardSummary'), query: 'Give me a summary of the dashboard' },
  { icon: Shield, label: t('aiAssistant.securityOverview'), query: 'What is the current security status?' },
]

export function AiAssistant() {
  const { t } = useTranslation()
  const [messages, setMessages] = useState<Message[]>([
    { role: 'assistant', content: 'Hello, I\'m **Sentinela AI Copilot**. I can help you monitor and manage your infrastructure. Ask me anything about your computers, alerts, security, or automation workflows.' },
  ])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => { bottomRef.current?.scrollIntoView({ behavior: 'smooth' }) }, [messages])

  const handleSend = async () => {
    if (!input.trim() || loading) return
    const userMsg: Message = { role: 'user', content: input.trim() }
    setMessages((prev) => [...prev, userMsg])
    setInput('')
    setLoading(true)

    setTimeout(() => {
      setMessages((prev) => [...prev, {
        role: 'assistant',
        content: `Here's what I found based on your query. I can see **${Math.floor(Math.random() * 100) + 50}** computers online, **${Math.floor(Math.random() * 10)}** critical alerts, and overall system health is **good**. Would you like me to drill down into any specific area?\n\n- [View Computers](/computers)\n- [View Alerts](/alerts)\n- [Security Dashboard](/security)`,
      }])
      setLoading(false)
    }, 1500)
  }

  return (
    <div className="flex h-[calc(100vh-8rem)] gap-6">
      {historyOpen && (
        <Card className="w-64 shrink-0">
          <CardHeader><CardTitle className="text-sm">{t('aiAssistant.history')}</CardTitle></CardHeader>
          <CardContent className="space-y-2 text-sm">
            {['Dashboard analysis', 'Alert summary', 'Security report'].map((h) => (
              <div key={h} className="p-2 rounded-md hover:bg-accent cursor-pointer text-muted-foreground">{h}</div>
            ))}
          </CardContent>
        </Card>
      )}

      <Card className="flex-1 flex flex-col">
        <CardHeader className="border-b border-border/50">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-xl bg-gradient-to-br from-primary to-blue-500 text-white">
                <Brain className="h-5 w-5" />
              </div>
              <div>
                <CardTitle className="text-base">{t('aiAssistant.aiCopilot')}</CardTitle>
                <p className="text-xs text-muted-foreground">{t('aiAssistant.poweredBySentinelaAi')}</p>
              </div>
            </div>
            <Button variant="ghost" size="sm" onClick={() => setHistoryOpen(!historyOpen)}>{t('aiAssistant.history')}</Button>
          </div>
        </CardHeader>

        <CardContent className="flex-1 overflow-y-auto p-4 space-y-4">
          {messages.map((msg, i) => (
            <div key={i} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
              <div
                className={`max-w-[80%] rounded-2xl p-4 ${
                  msg.role === 'user'
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-muted/50 border border-border/50'
                }`}
              >
                <div className="text-sm prose prose-invert max-w-none">
                  {msg.content.split('\n').map((line, j) => (
                    <p key={j} className={line.startsWith('- ') ? 'ml-2' : ''}>
                      {line.replace(/^\*\*(.*?)\*\*/, '')}
                      {line.match(/\*\*(.*?)\*\*/)?.[1] && <strong>{line.match(/\*\*(.*?)\*\*/)![1]}</strong>}
                      {line.replace(/\*\*(.*?)\*\*/, '').replace(/^[-*]\s*/, '')}
                    </p>
                  ))}
                </div>
              </div>
            </div>
          ))}
          {loading && (
            <div className="flex justify-start">
              <div className="bg-muted/50 border border-border/50 rounded-2xl p-4">
                <div className="flex items-center gap-2">
                  <Brain className="h-4 w-4 animate-pulse text-primary" />
                  <span className="text-sm text-muted-foreground">{t('aiAssistant.thinking')}</span>
                </div>
              </div>
            </div>
          )}
          <div ref={bottomRef} />
        </CardContent>

        <div className="p-4 border-t border-border/50 space-y-3">
          {messages.length === 1 && (
            <div className="flex flex-wrap gap-2">
              {suggestions(t).map((s) => (
                <button
                  key={s.label}
                  onClick={() => { setInput(s.query) }}
                  className="flex items-center gap-1.5 text-xs px-3 py-1.5 rounded-full border border-border/50 hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
                >
                  <s.icon className="h-3.5 w-3.5" />
                  {s.label}
                </button>
              ))}
            </div>
          )}

          <form
            onSubmit={(e) => { e.preventDefault(); handleSend() }}
            className="flex items-center gap-2"
          >
            <Input
              placeholder={t('aiAssistant.askAnything')}
              value={input}
              onChange={(e) => setInput(e.target.value)}
              className="flex-1"
            />
            <Button type="submit" size="icon" loading={loading}>
              <Send className="h-4 w-4" />
            </Button>
          </form>
        </div>
      </Card>
    </div>
  )
}
