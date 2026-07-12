import { Github, Twitter, Youtube, Instagram, Linkedin, Facebook, Twitch, Globe, MessageCircle, Music2 } from 'lucide-react'

const PLATFORM_ICONS: Record<string, typeof Globe> = {
  github: Github,
  twitter: Twitter,
  x: Twitter,
  youtube: Youtube,
  instagram: Instagram,
  linkedin: Linkedin,
  facebook: Facebook,
  twitch: Twitch,
  discord: MessageCircle,
  mastodon: MessageCircle,
  bluesky: MessageCircle,
  tiktok: Music2,
}

export function iconForPlatform(platform: string) {
  return PLATFORM_ICONS[platform.trim().toLowerCase()] ?? Globe
}
