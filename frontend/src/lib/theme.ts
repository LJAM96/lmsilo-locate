import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface ThemeState {
    isDark: boolean
    toggle: () => void
    setDark: (dark: boolean) => void
}

export const useTheme = create<ThemeState>()(
    persist(
        (set, get) => ({
            isDark: typeof window !== 'undefined'
                ? window.matchMedia('(prefers-color-scheme: dark)').matches
                : false,
            toggle: () => {
                const newValue = !get().isDark
                set({ isDark: newValue })
                updateHtmlClass(newValue)
            },
            setDark: (dark: boolean) => {
                set({ isDark: dark })
                updateHtmlClass(dark)
            },
        }),
        {
            name: 'locate-theme',
            onRehydrateStorage: () => (state) => {
                if (state) {
                    updateHtmlClass(state.isDark)
                }
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
if (typeof window !== 'undefined') {
    const stored = localStorage.getItem('locate-theme')
    if (stored) {
        try {
            const { state } = JSON.parse(stored)
            updateHtmlClass(state.isDark)
        } catch {
            updateHtmlClass(window.matchMedia('(prefers-color-scheme: dark)').matches)
        }
    }
}
