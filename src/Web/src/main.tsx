import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'sonner'
import App from './App'
import './styles/globals.css'
import './lib/i18n'

const savedTheme = localStorage.getItem('sentinela-theme')
if (savedTheme === 'light') document.documentElement.classList.remove('dark')
else if (savedTheme === 'dark') document.documentElement.classList.add('dark')

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30000,
    },
  },
})

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
        <Toaster position="top-right" richColors closeButton expand visibleToasts={5} />
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>,
)
