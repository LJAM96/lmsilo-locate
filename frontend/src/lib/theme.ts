import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface ThemeState {
    isDark: boolean
    toggle: () => void
    setDark: (dark: boolean) => void
}

// Helper to get theme from cookie synchronously
const getThemeFromCookie = () => {
    if (typeof document === 'undefined') return false
    const value = `; ${document.cookie}`
    const parts = value.split(`; lmsilo-theme=`)
    if (parts.length === 2) {
        const cookieVal = parts.pop()?.split(';').shift()
        return cookieVal === 'dark'
    }
    // Fallback to system preference
    return window.matchMedia('(prefers-color-scheme: dark)').matches
}

export const useTheme = create<ThemeState>()(
    persist(
        (set, get) => ({
            isDark: getThemeFromCookie(),
            toggle: () => {
                const newValue = !get().isDark
                set({ isDark: newValue })
                updateHtmlClass(newValue)
                const themeValue = newValue ? 'dark' : 'light'
                document.cookie = `lmsilo-theme=${themeValue}; path=/; max-age=31536000; SameSite=Lax`
            },
            setDark: (dark: boolean) => {
                set({ isDark: dark })
                updateHtmlClass(dark)
                const themeValue = dark ? 'dark' : 'light'
                document.cookie = `lmsilo-theme=${themeValue}; path=/; max-age=31536000; SameSite=Lax`
            },
        }),
        {
            name: 'locate-theme-storage',
            storage: {
                getItem: (name) => {
                    const value = `; ${document.cookie}`
                    const parts = value.split(`; lmsilo-theme=`)
                    if (parts.length === 2) {
                        const cookieVal = parts.pop()?.split(';').shift()
                        return JSON.stringify({ state: { isDark: cookieVal === 'dark' } })
                    }
                    return null
                },
                setItem: () => {}, // Handled in toggle/setDark
                removeItem: () => {},
            },
        }
    )
)

function updateHtmlClass(isDark: boolean) {
    if (typeof document !== 'undefined') {
        if (isDark) {
            document.documentElement.classList.add('dark')
        } else {
            document.documentElement.classList.remove('dark')
        }
    }
}

// Initialize on load
// Initialize on load
if (typeof window !== 'undefined') {
  const getCookie = (name: string) => {
    const value = `; ${document.cookie}`
    const parts = value.split(`; ${name}=`)
    if (parts.length === 2) return parts.pop()?.split(';').shift()
  }

  const cookieTheme = getCookie('lmsilo-theme')
  if (cookieTheme) {
      updateHtmlClass(cookieTheme === 'dark')
  } else {
      // Fallback to system preference
      updateHtmlClass(window.matchMedia('(prefers-color-scheme: dark)').matches)
  }
}
