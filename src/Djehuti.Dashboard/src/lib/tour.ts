import { driver } from 'driver.js'
import 'driver.js/dist/driver.css'
import type { TourStep } from '../types'

export function startTour(steps: TourStep[]) {
  if (steps.length === 0) return

  const d = driver({
    showProgress: true,
    animate: true,
    overlayOpacity: 0.55,
    stagePadding: 6,
    stageRadius: 6,
    allowClose: true,
    steps: steps.map((step) => ({
      element: step.target,
      popover: {
        title: step.title,
        description: step.text,
        side: (step.side as 'top' | 'bottom' | 'left' | 'right') ?? 'bottom',
        align: 'start',
      },
    })),
  })

  d.drive()
}

export function parseTourFromResponse(text: string): { tour: TourStep[] | null; cleaned: string } {
  const match = text.match(/```tour\s*([\s\S]*?)```/)
  if (!match) return { tour: null, cleaned: text }

  try {
    const tour = JSON.parse(match[1].trim()) as TourStep[]
    const cleaned = text.replace(match[0], '').trim()
    return { tour, cleaned }
  } catch {
    return { tour: null, cleaned: text }
  }
}
