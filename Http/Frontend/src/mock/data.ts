export const MOCK_PLAYER = { name: 'Liora Veil', world: 'Chocobo' };

export const MOCK_CHANNELS = {
  type: 'channels',
  channels: [
    { label: 'Say', shortLabel: 'Say', prefix: '/s ' },
    { label: 'Shout', shortLabel: 'Sh', prefix: '/sh ' },
    { label: 'Yell', shortLabel: 'Yell', prefix: '/y ' },
    { label: 'Party', shortLabel: 'Pty', prefix: '/p ' },
    { label: 'Alliance', shortLabel: 'Ally', prefix: '/a ' },
    { label: 'Free Company', shortLabel: 'FC', prefix: '/fc ' },
    { label: 'LS1: Starbound', shortLabel: 'LS1', prefix: '/l1 ' },
    { label: 'CWLS1: World Travelers', shortLabel: 'CW1', prefix: '/cw1 ' },
    { label: 'Novice Network', shortLabel: 'NN', prefix: '/n ' },
  ],
};

const ALL_CHANNELS = [10, 11, 12, 13, 14, 15, 16, 24, 27, 28, 29, 30, 37, 56, 57, 61, 62];

export const MOCK_SETTINGS = {
  theme: 'dark',
  fontFamily: 'Inter',
  fontSize: 14,
  italicizeSystem: true,
  useColoredBackground: true,
  largeLinkPreviews: false,
  sendDelayEnabled: true,
  sendDelaySeconds: 3,
  sendDelayAlwaysQueue: false,
  disabledChannels: [],
  trustedDomains: [],
  filters: [
    {
      name: 'General',
      showChannelTypes: ALL_CHANNELS,
      defaultSendPrefix: null,
      notifyUnread: false,
    },
    {
      name: 'The-Crew',
      showChannelTypes: [10, 14, 15, 24],
      defaultSendPrefix: null,
      notifyUnread: true,
    },
    { name: 'Static', showChannelTypes: [14], defaultSendPrefix: '/p ', notifyUnread: true },
    { name: 'FC', showChannelTypes: [24], defaultSendPrefix: '/fc ', notifyUnread: true },
    { name: 'Hunt-Train', showChannelTypes: [11, 30], defaultSendPrefix: null, notifyUnread: true },
    {
      name: 'Social',
      showChannelTypes: [10, 11, 30, 28, 29],
      defaultSendPrefix: null,
      notifyUnread: false,
    },
  ],
  folders: [
    { name: 'FILTERS', filters: ['General', 'The-Crew', 'Static'] },
    { name: 'COMMUNITY', filters: ['FC', 'Hunt-Train', 'Social'] },
  ],
};

export const MOCK_FILTER_MODE = {
  characterKey: `${MOCK_PLAYER.name}@${MOCK_PLAYER.world}`,
  isPersonal: false,
  hasExistingFile: false,
  personalFilterCharacters: ['Baldur Grimstone@Gungnir', 'Finn Ashwood@Gungnir'],
};

export const MOCK_CHARACTERS = [
  {
    key: 'Baldur Grimstone@Gungnir',
    filters: [
      {
        name: 'General',
        showChannelTypes: ALL_CHANNELS,
        defaultSendPrefix: null,
        notifyUnread: false,
      },
      { name: 'Static', showChannelTypes: [14], defaultSendPrefix: '/p ', notifyUnread: true },
      { name: 'FC', showChannelTypes: [24], defaultSendPrefix: '/fc ', notifyUnread: true },
    ],
    folders: [{ name: 'Main', filters: ['General', 'Static', 'FC'] }],
  },
  {
    key: 'Finn Ashwood@Gungnir',
    filters: [
      {
        name: 'General',
        showChannelTypes: ALL_CHANNELS,
        defaultSendPrefix: null,
        notifyUnread: false,
      },
      { name: 'Static', showChannelTypes: [14], defaultSendPrefix: '/p ', notifyUnread: true },
      {
        name: 'The-Crew',
        showChannelTypes: [10, 14, 15, 24],
        defaultSendPrefix: null,
        notifyUnread: true,
      },
    ],
    folders: [
      { name: 'FILTERS', filters: ['General', 'Static'] },
      { name: 'SOCIAL', filters: ['The-Crew'] },
    ],
  },
];

const LOCAL = MOCK_PLAYER.name;
const WORLD = MOCK_PLAYER.world;
const now = Date.now();
const t = (minutesAgo: number) => now - minutesAgo * 60000;
const text = (s: string) => [{ Type: 'text', Text: s }];
const at = (s: string) => [
  { Type: 'icon' as const, IconId: 54 },
  { Type: 'text' as const, Text: s },
  { Type: 'icon' as const, IconId: 55 },
];

export const MOCK_PLAYER_AVATAR_URL = '/avatars/avatar0.png';

export const MOCK_AVATAR_POOL = [
  '/avatars/avatar1.png',
  '/avatars/avatar2.png',
  '/avatars/avatar3.png',
  '/avatars/avatar4.png',
  '/avatars/avatar5.png',
];

const AVATAR_BY_NAME: Record<string, string> = {
  'Liora Veil': '/avatars/avatar0.png',
  'Caelum Arke': '/avatars/avatar1.png',
  'Nael Orenthal': '/avatars/avatar2.png',
  'Thalise Maron': '/avatars/avatar3.png',
  'Finn Ashwood': '/avatars/avatar4.png',
  'Baldur Grimstone': '/avatars/avatar5.png',
};

export function getAvatarForName(name: string): string {
  const mapped = AVATAR_BY_NAME[name];
  if (mapped) return mapped;

  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = (hash * 31 + name.charCodeAt(i)) >>> 0;
  }
  return MOCK_AVATAR_POOL[hash % MOCK_AVATAR_POOL.length];
}

export const MOCK_MESSAGES = [
  // Pre-dungeon chatter in Say
  {
    Type: 10,
    SenderName: 'Caelum Arke',
    SenderWorld: 'Carbuncle',
    MessagePayloads: text('anyone done the new unreal yet? is it actually that hard'),
    Timestamp: t(22),
  },
  {
    Type: 10,
    SenderName: LOCAL,
    SenderWorld: WORLD,
    MessagePayloads: text("cleared it last week, took us like 4 hours but it's really fun"),
    Timestamp: t(21),
  },
  // Shout
  {
    Type: 11,
    SenderName: 'Nael Orenthal',
    SenderWorld: 'Phantom',
    MessagePayloads: text('LFM P12S prog, need 1 healer — must know phase 1'),
    Timestamp: t(18),
  },
  // CWLS coordination
  {
    Type: 37,
    SenderName: 'Thalise Maron',
    SenderWorld: 'Gungnir',
    MessagePayloads: text('who is on for hunt train tonight? fairy b in 30'),
    Timestamp: t(15),
  },
  {
    Type: 37,
    SenderName: 'Finn Ashwood',
    SenderWorld: 'Gungnir',
    MessagePayloads: text("I'll come after reclears, might be a bit late"),
    Timestamp: t(14),
  },
  // Static party sequence
  {
    Type: 14,
    SenderName: LOCAL,
    SenderWorld: WORLD,
    MessagePayloads: text(
      'lol finally. ok strats: Baldur takes the boss north, Finn and I spread east/west on the first AOE',
    ),
    Timestamp: t(12),
  },
  {
    Type: 14,
    SenderName: 'Baldur Grimstone',
    SenderWorld: 'Gungnir',
    MessagePayloads: at('Got it!'),
    Timestamp: t(11),
  },
  {
    Type: 14,
    SenderName: 'Finn Ashwood',
    SenderWorld: 'Gungnir',
    MessagePayloads: at('Got it!'),
    Timestamp: t(11),
  },
  {
    Type: 14,
    SenderName: LOCAL,
    SenderWorld: WORLD,
    MessagePayloads: text(
      "wait Finn your auto-translate said 'Got it' not 'On my way' — stack on me for the tankbuster",
    ),
    Timestamp: t(10),
  },
  {
    Type: 14,
    SenderName: 'Finn Ashwood',
    SenderWorld: 'Gungnir',
    MessagePayloads: at('Oops!'),
    Timestamp: t(9),
  },
  {
    Type: 14,
    SenderName: 'Baldur Grimstone',
    SenderWorld: 'Gungnir',
    MessagePayloads: text('someone toss a Phoenix Down lol'),
    Timestamp: t(8),
  },
  // FC spectating
  {
    Type: 24,
    SenderName: 'Thalise Maron',
    SenderWorld: WORLD,
    MessagePayloads: text('lmaooo watching from the FC room, gl in there!!!'),
    Timestamp: t(8),
  },
  // Back to party
  {
    Type: 14,
    SenderName: LOCAL,
    SenderWorld: WORLD,
    MessagePayloads: text('THIS IS FINE. ok from the top'),
    Timestamp: t(7),
  },
  // Post-clear LS
  {
    Type: 16,
    SenderName: 'Caelum Arke',
    SenderWorld: 'Carbuncle',
    MessagePayloads: text(
      'hey did you guys see this? https://na.finalfantasyxiv.com/lodestone/topics/detail/07320affa7e0fcd9685afcbe54fbf55405b6d822/',
    ),
    Timestamp: t(4),
  },
  // Tell incoming from static member
  {
    Type: 13,
    SenderName: 'Finn Ashwood',
    SenderWorld: 'Gungnir',
    MessagePayloads: [
      { Type: 'text' as const, Text: 'heyyy!! ' },
      ...at('Anabaseios: The Twelfth Circle (Savage)'),
      {
        Type: 'text' as const,
        Text: ' tonight, 5 slot just opened — you down? prog P3 every week ',
      },
    ],
    Timestamp: t(3),
  },
  // Tell outgoing
  {
    Type: 12,
    SenderName: LOCAL,
    SenderWorld: WORLD,
    RecipientName: 'Finn Ashwood',
    RecipientWorld: 'Gungnir',
    MessagePayloads: text('yes omg, same time as usual?'),
    Timestamp: t(2),
  },
  // Celebration in say
  {
    Type: 10,
    SenderName: LOCAL,
    SenderWorld: WORLD,
    MessagePayloads: text('we did it!! gg everyone'),
    Timestamp: t(1),
  },
  // Emote logs post-clear
  {
    Type: 29,
    SenderName: LOCAL,
    SenderWorld: WORLD,
    MessagePayloads: text('Liora Veil cheers!'),
    Timestamp: t(0),
  },
  {
    Type: 29,
    SenderName: 'Finn Ashwood',
    SenderWorld: 'Gungnir',
    MessagePayloads: text('Finn Ashwood jubilates with Liora Veil!'),
    Timestamp: t(0),
  },
  {
    Type: 29,
    SenderName: 'Baldur Grimstone',
    SenderWorld: 'Gungnir',
    MessagePayloads: text('Baldur Grimstone performs a victory pose.'),
    Timestamp: t(0),
  },
];

// ── AI assistant demo data ───────────────────────────────────────
export const MOCK_AI_SETTINGS = {
  enabled: true,
  apiKeySet: true,
  apiBaseUrl: 'https://api.openai.com/v1',
  defaultModel: 'gpt-4o-mini',
  contextMessageCount: 20,
  timeoutSeconds: 60,
  agents: [
    {
      id: 'agent-social',
      name: 'Social Butler',
      systemPrompt:
        'You are a friendly, concise party assistant in Final Fantasy XIV. Keep replies short and natural.',
      model: '',
      temperature: 0.7,
      maxTokens: 300,
    },
  ],
  rules: [
    {
      id: 'rule-party',
      name: 'Party helper',
      enabled: true,
      agentId: 'agent-social',
      mode: 'options',
      channelTypes: [14],
      cooldownSeconds: 10,
      maxPerMinute: 6,
      optionsCount: 3,
    },
    {
      id: 'rule-tell',
      name: 'Tell concierge',
      enabled: true,
      agentId: 'agent-social',
      mode: 'suggestion',
      channelTypes: [13],
      cooldownSeconds: 5,
      maxPerMinute: 10,
      optionsCount: 1,
    },
  ],
};

export const MOCK_AI_SUGGESTIONS = [
  {
    id: 'suggestion-party-1',
    ruleId: 'rule-party',
    ruleName: 'Party helper',
    agentName: 'Social Butler',
    mode: 'options',
    channelType: 14,
    channelLabel: 'Party',
    senderName: 'Baldur Grimstone',
    senderWorld: 'Gungnir',
    sourceText: 'someone toss a Phoenix Down lol',
    candidates: [
      'On it — Phoenix Down incoming!',
      'I used mine on the last wipe. Anyone else have one?',
      'Give me a sec, checking my inventory.',
    ],
    createdAt: Date.now() - 20_000,
  },
];

export const MOCK_AI_ACTIVITY = [
  {
    timestamp: Date.now() - 60_000,
    kind: 'auto-reply',
    message: 'Finn Ashwood: Yes! Same time as usual works for me.',
  },
  {
    timestamp: Date.now() - 180_000,
    kind: 'info',
    message: 'Generated a reply suggestion for a Tell from Finn Ashwood.',
  },
];
