import type {
  MlmceParticipantConfig,
  MlmceSessionKind,
  MlmceThresholdConfig,
  MlmceTurnMode,
} from '../../types'

export const buildMlmceConfigPreview = (
  participants: MlmceParticipantConfig[],
  moderatorModel: string,
  moderatorProfileVersion: string,
  turnMode: MlmceTurnMode,
  sessionKind: MlmceSessionKind,
  seedPrompt: string,
  thresholds: MlmceThresholdConfig,
) => ({
  participants: participants.map((participant) => ({
    id: participant.id,
    roleLabel: participant.roleLabel,
    modelId: participant.modelId,
  })),
  moderator: {
    modelId: moderatorModel,
    initializationProfileVersion: moderatorProfileVersion,
  },
  turnTakingMode: turnMode,
  sessionKind,
  seedPrompt,
  interventionThresholds: thresholds,
})

export const validateMlmceConfig = (
  participants: MlmceParticipantConfig[],
  moderatorModel: string,
  seedPrompt: string,
  thresholds: MlmceThresholdConfig,
) => {
  const issues: string[] = []

  if (participants.length < 2) {
    issues.push('At least two participants are required for MLMCE sessions.')
  }

  for (const participant of participants) {
    if (!participant.id.trim()) {
      issues.push('Each participant needs an id.')
    }
    if (!participant.roleLabel.trim()) {
      issues.push('Each participant needs a role label.')
    }
    if (!participant.modelId.trim()) {
      issues.push('Each participant needs a model id.')
    }
  }

  const ids = participants.map((participant) => participant.id.trim()).filter(Boolean)
  if (new Set(ids).size !== ids.length) {
    issues.push('Participant ids must be unique.')
  }

  if (!moderatorModel.trim()) {
    issues.push('Moderator model id is required.')
  }

  if (!seedPrompt.trim()) {
    issues.push('Seed prompt is required.')
  }

  if (thresholds.attractorWindow < 1) {
    issues.push('Attractor window must be at least 1.')
  }

  if (thresholds.leakageBudgetFraction <= 0 || thresholds.leakageBudgetFraction > 1) {
    issues.push('Leakage budget fraction must be greater than 0 and at most 1.')
  }

  return issues
}
