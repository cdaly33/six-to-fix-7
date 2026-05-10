import React, { useMemo, useState } from "react";
import { motion } from "framer-motion";
import {
  ArrowRight,
  BookOpen,
  Boxes,
  Briefcase,
  Building2,
  CheckCircle2,
  ChevronRight,
  ClipboardList,
  Gauge,
  Layers3,
  LayoutDashboard,
  LineChart,
  Megaphone,
  MessageSquare,
  Package,
  Palette,
  Phone,
  Settings,
  Star,
  Target,
  TrendingUp,
  Users,
  Wrench,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";

const sectionMeta = {
  homepage: { label: "Homepage", icon: BookOpen, color: "bg-yellow-400 text-slate-900" },
  dashboard: { label: "Dashboard", icon: LayoutDashboard, color: "bg-yellow-400 text-slate-900" },
  brand: { label: "Brand", icon: Palette, color: "bg-rose-500 text-white" },
  customer: { label: "Customer", icon: Users, color: "bg-blue-500 text-white" },
  offering: { label: "Offering", icon: Boxes, color: "bg-violet-500 text-white" },
  communication: { label: "Communication", icon: MessageSquare, color: "bg-orange-500 text-white" },
  sales: { label: "Sales", icon: TrendingUp, color: "bg-green-500 text-white" },
  management: { label: "Management", icon: Settings, color: "bg-slate-500 text-white" },
  templates: { label: "Templates", icon: ClipboardList, color: "bg-amber-500 text-slate-900" },
};

const dashboardCards = [
  { key: "brand", subtitle: "Strategy · Execution · Templates", progress: 42 },
  { key: "customer", subtitle: "Strategy · Execution · Templates", progress: 58 },
  { key: "offering", subtitle: "Strategy · Execution · Templates", progress: 51 },
  { key: "communication", subtitle: "Strategy · Execution · Templates", progress: 67 },
  { key: "sales", subtitle: "Strategy · Execution · Templates", progress: 63 },
  { key: "management", subtitle: "Strategy · Execution · Templates", progress: 39 },
];

const sectionContent = {
  brand: {
    title: "Brand Strategy",
    subtitle: "Build a powerful brand identity and positioning",
    strategy: [
      {
        title: "Brand Positioning",
        points: [
          "Clarify the category, audience, and point of view that make the company memorable.",
          "Define the promise the brand must consistently deliver across every channel.",
          "Anchor messaging around trust, expertise, and measurable business value.",
        ],
      },
      {
        title: "Identity System",
        points: [
          "Establish a visual system with navy, gold, ivory, and a restrained editorial feel.",
          "Pair serif headlines with clean sans-serif UI for authority and clarity.",
          "Create reusable standards for iconography, cards, buttons, and page rhythm.",
        ],
      },
      {
        title: "Brand Experience",
        points: [
          "Treat the website as a brand platform rather than a static brochure.",
          "Use every screen to reinforce trust, clarity, and forward momentum.",
          "Keep tone practical, confident, and strategic instead of promotional fluff.",
        ],
      },
    ],
    execution: [
      "Develop a one-page brand guide for voice, typography, and UI usage.",
      "Build section hero blocks with consistent icon framing and hierarchy.",
      "Standardize section tabs for Strategy, Execution Blueprint, Templates, and Examples.",
      "Roll the same system into dashboards, templates, exports, and future client playbooks.",
    ],
    templates: [
      "Brand Positioning Worksheet",
      "Messaging House",
      "Voice and Tone Guide",
      "Homepage Copy Brief",
      "Visual QA Checklist",
    ],
    examples: [
      "Homepage hero aligned to the strategic-business-platform promise.",
      "Section page that turns framework concepts into actionable modules.",
      "Reusable card layouts for playbook chapters and downloadable assets.",
    ],
    metrics: [
      ["Brand clarity score", "8.4/10"],
      ["Homepage CTA clicks", "+31%"],
      ["Avg. time on playbook pages", "5m 12s"],
    ],
  },
  customer: {
    title: "Customer Strategy",
    subtitle: "Understand audience segments, acquisition priorities, and retention motion",
    strategy: [
      {
        title: "Segmentation Model",
        points: [
          "Separate service agreement customers, one-time buyers, commercial accounts, prospects, and lapsed accounts.",
          "Use value, urgency, and lifecycle stage to personalize content and outreach.",
          "Align the CRM structure to the same strategic segments used in the playbook.",
        ],
      },
      {
        title: "Ideal Customer Profile",
        points: [
          "Define the right customer instead of treating every closed sale as equal.",
          "Map pain points, buying triggers, objections, and likely upgrade paths.",
          "Prioritize customers with strong fit, long-term value, and lower service friction.",
        ],
      },
      {
        title: "Lifecycle Design",
        points: [
          "Design onboarding, renewal, win-back, and loyalty moments up front.",
          "Create a path from prospect to customer to advocate.",
          "Measure customer value over time, not just initial acquisition.",
        ],
      },
    ],
    execution: [
      "Create dynamic CRM lists by segment and lifecycle stage.",
      "Trigger onboarding and service-agreement nurture sequences.",
      "Add win-back campaigns for lapsed accounts with tailored offers.",
      "Expose retention risk and opportunity scorecards inside the dashboard.",
    ],
    templates: [
      "ICP Builder",
      "Persona Snapshot",
      "Segment Messaging Matrix",
      "Win-Back Campaign Brief",
      "Onboarding Sequence Planner",
    ],
    examples: [
      "VIP nurture for high-LTV accounts.",
      "Spring tune-up campaign for residential prospects.",
      "Commercial property manager outreach sequence.",
    ],
    metrics: [
      ["Segmented contacts", "847"],
      ["Service agreement enrollments", "12 new"],
      ["Win-back reactivations", "14"],
    ],
  },
  offering: {
    title: "Offering Strategy",
    subtitle: "Package services for clarity, recurring revenue, and easier buying decisions",
    strategy: [
      {
        title: "Service Packaging",
        points: [
          "Move from one-off transactions toward named, tiered offerings.",
          "Use Good / Better / Best architecture to simplify comparison and upsell.",
          "Design bundles around outcomes, convenience, and reduced risk.",
        ],
      },
      {
        title: "Recurring Revenue",
        points: [
          "Prioritize plans and agreements that improve predictability and enterprise value.",
          "Frame premium tiers around speed, savings, and peace of mind.",
          "Make annual value visible in both sales conversations and website UI.",
        ],
      },
      {
        title: "Digital Presentation",
        points: [
          "Translate offerings into website cards, comparison tables, and estimate flows.",
          "Reduce friction with clear CTAs and transparent next steps.",
          "Support the sales process with mobile-friendly leave-behinds and proposal templates.",
        ],
      },
    ],
    execution: [
      "Publish pricing and feature comparisons for Silver, Gold, and Platinum tiers.",
      "Create a promotion module for seasonal campaigns.",
      "Add a fast estimate-request flow with segmented follow-up.",
      "Equip sales with proposal templates and in-home comparison sheets.",
    ],
    templates: [
      "Offer Comparison Table",
      "Proposal Builder",
      "Promotion Landing Page",
      "Upsell Script",
      "Commercial Scope-of-Work Template",
    ],
    examples: [
      "Comfort Care plan comparison.",
      "Spring Tune-Up limited-time promotion.",
      "Commercial recurring maintenance package.",
    ],
    metrics: [
      ["Average plan value", "$229"],
      ["Estimate requests", "23"],
      ["Commercial proposals won", "2"],
    ],
  },
  communication: {
    title: "Communication Strategy",
    subtitle: "Deliver the right message at the right time across owned and paid channels",
    strategy: [
      {
        title: "Channel Architecture",
        points: [
          "Use email for nurture, SMS for urgency, social for authority, and direct mail for local activation.",
          "Keep the voice consistent even when the format changes.",
          "Use communication as a system, not a collection of one-off campaigns.",
        ],
      },
      {
        title: "Cadence Planning",
        points: [
          "Map monthly newsletters, service reminders, appointment texts, and renewal notices.",
          "Balance evergreen education with seasonal promotions.",
          "Define ownership and response SLAs for every channel.",
        ],
      },
      {
        title: "Owned Audience Growth",
        points: [
          "Grow lists and opt-ins from forms, estimates, lead magnets, and service interactions.",
          "Treat email and SMS audiences as long-term assets.",
          "Design content that builds trust before it asks for action.",
        ],
      },
    ],
    execution: [
      "Launch a monthly newsletter block with seasonal education modules.",
      "Configure appointment reminders, dispatch texts, and renewal nudges.",
      "Create channel-specific campaign cards with copy status and due dates.",
      "Track engagement and bookings from communication flows.",
    ],
    templates: [
      "Newsletter Template",
      "SMS Reminder Sequence",
      "Campaign Calendar",
      "Direct Mail Brief",
      "Owner Video Email Script",
    ],
    examples: [
      "January cold-snap emergency text blast.",
      "Spring AC tune-up newsletter.",
      "Localized postcard campaign with QR booking code.",
    ],
    metrics: [
      ["Average email open rate", "41%"],
      ["SMS read rate", "63%"],
      ["Direct mail response", "2.8%"],
    ],
  },
  sales: {
    title: "Sales Strategy",
    subtitle: "Turn demand into predictable revenue with clear process and follow-through",
    strategy: [
      {
        title: "Lead-to-Close System",
        points: [
          "Design a visible process from inquiry to booked appointment to quote to close.",
          "Use SLAs to speed response time and reduce leakage.",
          "Track pipeline stages so opportunities do not disappear in inboxes.",
        ],
      },
      {
        title: "Point-of-Service Selling",
        points: [
          "Support technicians with simple enrollments, upgrade prompts, and leave-behinds.",
          "Use short scripts, not complicated playbooks, at the moment of decision.",
          "Capture equipment age, objections, and follow-up dates in CRM.",
        ],
      },
      {
        title: "Follow-Up Discipline",
        points: [
          "Automate day-1, day-3, and day-7 quote follow-up touches.",
          "Give sales context, not just reminders, so every outreach feels informed.",
          "Make next-best-action visible on every deal card.",
        ],
      },
    ],
    execution: [
      "Add a live pipeline board with deal stages and aging markers.",
      "Deploy quote follow-up automations and task prompts.",
      "Create a simple same-day proposal send checklist.",
      "Bundle scripts, objection handling, and proof points into one screen.",
    ],
    templates: [
      "Discovery Call Guide",
      "Quote Follow-Up Sequence",
      "Technician Enrollment Script",
      "Deal Stage Definitions",
      "Proposal Email Draft",
    ],
    examples: [
      "5-minute response SLA for inbound leads.",
      "Same-day quote delivery workflow.",
      "Technician upsell card for IAQ and plan enrollment.",
    ],
    metrics: [
      ["Inbound response SLA", "5 min"],
      ["Quote follow-up completion", "89%"],
      ["Pipeline visibility score", "A-"],
    ],
  },
  management: {
    title: "Management",
    subtitle: "Operationalize the framework with ownership, KPIs, and execution rhythm",
    strategy: [
      {
        title: "Operating System",
        points: [
          "Assign an owner to each pillar and create a weekly review cadence.",
          "Tie marketing, sales, and service activity to the same business scorecard.",
          "Turn strategy into repeatable systems instead of heroic individual effort.",
        ],
      },
      {
        title: "KPI Governance",
        points: [
          "Track a few metrics that actually change decisions.",
          "Pair activity metrics with outcome metrics to avoid vanity reporting.",
          "Review progress monthly by pillar and quarterly by business objective.",
        ],
      },
      {
        title: "Team Alignment",
        points: [
          "Keep leadership, marketing, sales, and operations using the same language.",
          "Make priorities visible so teams know what matters now and what comes next.",
          "Document standards so the system scales without constant explanation.",
        ],
      },
    ],
    execution: [
      "Create an executive scorecard with growth, customer, and operational KPIs.",
      "Publish weekly action items by section owner.",
      "Add meeting notes, blockers, and dependencies to each section view.",
      "Expose completion progress to reinforce accountability.",
    ],
    templates: [
      "Weekly Leadership Agenda",
      "Quarterly KPI Review",
      "Section Owner Scorecard",
      "Project Priority Matrix",
      "Decision Log",
    ],
    examples: [
      "Weekly cross-functional review by pillar.",
      "Quarterly scorecard tied to strategic objectives.",
      "Owner-based execution tracker for all playbook actions.",
    ],
    metrics: [
      ["Weekly review cadence", "On track"],
      ["Template adoption", "74%"],
      ["Execution ownership", "6 / 6 pillars"],
    ],
  },
  templates: {
    title: "Template Library",
    subtitle: "A reusable toolkit for strategy, execution, and client delivery",
    strategy: [
      {
        title: "Framework Tools",
        points: [
          "Collect reusable worksheets, scorecards, campaign briefs, and proposal shells.",
          "Organize templates by section and by use case.",
          "Keep naming, layout, and quality control consistent.",
        ],
      },
      {
        title: "Delivery Efficiency",
        points: [
          "Speed up implementation by giving teams ready-to-use starting points.",
          "Reduce reinvention across clients and campaigns.",
          "Pair every template with a short usage note and owner.",
        ],
      },
      {
        title: "Systemization",
        points: [
          "Turn best practices into assets the team can deploy repeatedly.",
          "Keep template health visible with versioning and update dates.",
          "Support both internal delivery and client-facing output.",
        ],
      },
    ],
    execution: [
      "Create filters for section, format, and status.",
      "Add preview, duplicate, and export actions to each template card.",
      "Show popularity, last updated date, and recommended next step.",
      "Group templates into starter packs for common use cases.",
    ],
    templates: [
      "Brand Strategy Pack",
      "Customer Segmentation Kit",
      "Offer Comparison Pack",
      "Communication Calendar Kit",
      "Sales Enablement Pack",
      "Management Scorecard Pack",
    ],
    examples: [
      "Starter bundle for launching a new client playbook.",
      "Seasonal campaign kit for HVAC demand spikes.",
      "Leadership reporting bundle for monthly reviews.",
    ],
    metrics: [
      ["Active templates", "36"],
      ["Most used pack", "Sales Enablement"],
      ["Average time saved", "5.2 hrs / wk"],
    ],
  },
};

function Logo() {
  return (
    <div className="flex items-center gap-3 text-white">
      <div className="grid h-7 w-7 place-items-center rounded-md border border-yellow-400/60 text-yellow-400">
        <BookOpen className="h-4 w-4" />
      </div>
      <span className="font-serif text-[28px] leading-none">StrategyHub</span>
    </div>
  );
}

function NavItem({ active, label, icon: Icon, onClick }) {
  return (
    <button
      onClick={onClick}
      className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm transition ${
        active
          ? "bg-slate-800 text-yellow-300"
          : "text-slate-300 hover:bg-slate-900 hover:text-white"
      }`}
    >
      <Icon className="h-4 w-4" />
      <span>{label}</span>
    </button>
  );
}

function Shell({ active, setActive, children }) {
  const sidebarItems = ["dashboard", "brand", "customer", "offering", "communication", "sales", "management", "templates"];

  return (
    <div className="min-h-screen bg-[#f3f3f5] text-slate-900">
      <div className="grid min-h-screen grid-cols-[260px_1fr]">
        <aside className="flex min-h-screen flex-col border-r border-slate-800 bg-[#081432] px-4 py-5">
          <div className="border-b border-slate-800 pb-5">
            <Logo />
          </div>

          <div className="pt-4 text-xs uppercase tracking-[0.18em] text-slate-500">Playbook</div>
          <div className="mt-3 space-y-1.5">
            {sidebarItems.map((item) => {
              const meta = sectionMeta[item];
              return (
                <NavItem
                  key={item}
                  active={active === item}
                  label={meta.label}
                  icon={meta.icon}
                  onClick={() => setActive(item)}
                />
              );
            })}
          </div>

          <div className="mt-auto space-y-3 border-t border-slate-800 pt-5 text-slate-300">
            <div>
              <div className="font-medium text-white">Terry Sullivan</div>
              <div className="text-sm text-slate-400">Client</div>
            </div>
            <button className="text-sm text-slate-400 transition hover:text-white">Sign Out</button>
          </div>
        </aside>

        <main className="min-h-screen">
          <div className="h-14 border-b border-slate-200/80 bg-[#f5f5f7]" />
          <div className="p-6 md:p-8">{children}</div>
        </main>
      </div>
    </div>
  );
}

function Homepage({ setActive }) {
  const pillarCards = ["brand", "customer", "offering", "communication", "sales", "management"];

  return (
    <div className="min-h-screen bg-[#efeff2] text-slate-900">
      <header className="border-b border-white/10 bg-[#040b20] text-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
          <Logo />
          <div className="flex items-center gap-4 text-sm">
            <button className="text-white/80 hover:text-white">Sign In</button>
            <Button className="rounded-xl bg-yellow-400 px-5 text-slate-900 hover:bg-yellow-300">Get Started</Button>
          </div>
        </div>
      </header>

      <section className="bg-[radial-gradient(circle_at_center,_rgba(31,58,147,0.35),_transparent_50%),linear-gradient(90deg,#07112e_0%,#162856_100%)] text-white">
        <div className="mx-auto max-w-7xl px-6 py-24 text-center md:py-28">
          <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="mx-auto mb-8 inline-flex rounded-full bg-yellow-400 px-5 py-2 text-xs font-semibold uppercase tracking-[0.18em] text-slate-900">
            Strategic Business Platform
          </motion.div>
          <motion.h1 initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }} className="mx-auto max-w-4xl font-serif text-5xl leading-tight md:text-7xl">
            Your Complete <span className="text-yellow-400">Business Playbook</span>
          </motion.h1>
          <motion.p initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="mx-auto mt-8 max-w-3xl text-lg text-white/75 md:text-2xl">
            Transform strategy into execution with personalized playbooks covering every dimension of business growth.
          </motion.p>
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.15 }} className="mt-10 flex items-center justify-center gap-4">
            <Button onClick={() => setActive("dashboard")} className="h-14 rounded-2xl bg-yellow-400 px-8 text-lg font-semibold text-slate-900 hover:bg-yellow-300">
              Start Your Playbook <ArrowRight className="ml-2 h-5 w-5" />
            </Button>
          </motion.div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-6 py-20">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="font-serif text-4xl md:text-5xl">Six Pillars of Business Strategy</h2>
          <p className="mt-4 text-lg text-slate-500">
            A comprehensive framework covering every strategic dimension your business needs to thrive.
          </p>
        </div>

        <div className="mt-12 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {pillarCards.map((key) => {
            const meta = sectionMeta[key];
            const Icon = meta.icon;
            return (
              <motion.button
                key={key}
                whileHover={{ y: -4 }}
                onClick={() => setActive(key)}
                className="rounded-3xl border border-slate-200 bg-white p-6 text-left shadow-sm transition hover:shadow-lg"
              >
                <div className={`mb-6 grid h-14 w-14 place-items-center rounded-2xl ${meta.color}`}>
                  <Icon className="h-6 w-6" />
                </div>
                <h3 className="font-serif text-2xl">{meta.label} Strategy</h3>
                <p className="mt-3 text-base text-slate-500">
                  {key === "brand" && "Define your brand identity, positioning, and value proposition."}
                  {key === "customer" && "Understand audience segments, ideal customers, and buyer personas."}
                  {key === "offering" && "Craft compelling products and service portfolios with recurring value."}
                  {key === "communication" && "Build messaging frameworks and content systems across channels."}
                  {key === "sales" && "Design sales processes, pipeline visibility, and revenue growth plans."}
                  {key === "management" && "Establish execution rhythm, scorecards, and operational alignment."}
                </p>
              </motion.button>
            );
          })}
        </div>
      </section>
    </div>
  );
}

function Dashboard({ setActive }) {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-serif text-5xl">Welcome back, Terry Sullivan</h1>
        <p className="mt-2 text-lg text-slate-500">Your strategic business playbook overview</p>
      </div>

      <Card className="rounded-3xl border-slate-200 shadow-sm">
        <CardHeader>
          <CardTitle className="font-serif text-2xl">Overall Progress</CardTitle>
          <CardDescription>Track execution across your six strategic pillars.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Progress value={54} className="h-3" />
          <div className="flex flex-wrap gap-2">
            {dashboardCards.map((card) => (
              <Badge key={card.key} variant="secondary" className="rounded-full bg-slate-100 px-3 py-1 text-slate-600">
                {sectionMeta[card.key].label} {card.progress}%
              </Badge>
            ))}
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-6 xl:grid-cols-3">
        {dashboardCards.map((card) => {
          const meta = sectionMeta[card.key];
          const Icon = meta.icon;
          return (
            <button key={card.key} onClick={() => setActive(card.key)} className="text-left">
              <Card className="h-full rounded-3xl border-slate-200 shadow-sm transition hover:-translate-y-1 hover:shadow-lg">
                <CardContent className="space-y-5 p-6">
                  <div className={`grid h-14 w-14 place-items-center rounded-2xl ${meta.color}`}>
                    <Icon className="h-6 w-6" />
                  </div>
                  <div>
                    <div className="font-serif text-3xl">{meta.label}</div>
                    <div className="mt-1 text-sm text-slate-500">{card.subtitle}</div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Progress value={card.progress} className="h-2.5 flex-1" />
                    <span className="text-sm font-medium text-slate-500">{card.progress}%</span>
                    <ArrowRight className="h-4 w-4 text-slate-400" />
                  </div>
                </CardContent>
              </Card>
            </button>
          );
        })}
      </div>
    </div>
  );
}

function MetricGrid({ metrics }) {
  return (
    <div className="grid gap-4 md:grid-cols-3">
      {metrics.map(([label, value]) => (
        <Card key={label} className="rounded-3xl border-slate-200 shadow-sm">
          <CardContent className="p-6">
            <div className="text-sm uppercase tracking-[0.16em] text-slate-500">{label}</div>
            <div className="mt-3 font-serif text-4xl">{value}</div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function SectionPanel({ name }) {
  const data = sectionContent[name];
  const meta = sectionMeta[name];
  const Icon = meta.icon;
  const accentMap = {
    brand: "from-rose-50 to-white",
    customer: "from-blue-50 to-white",
    offering: "from-violet-50 to-white",
    communication: "from-orange-50 to-white",
    sales: "from-green-50 to-white",
    management: "from-slate-100 to-white",
    templates: "from-amber-50 to-white",
  };

  return (
    <div className="space-y-6">
      <div className={`rounded-[32px] border border-slate-200 bg-gradient-to-br ${accentMap[name]} p-7 shadow-sm`}>
        <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-start gap-5">
            <div className={`grid h-16 w-16 place-items-center rounded-2xl ${meta.color}`}>
              <Icon className="h-7 w-7" />
            </div>
            <div>
              <h1 className="font-serif text-5xl leading-tight">{data.title}</h1>
              <p className="mt-2 text-lg text-slate-500">{data.subtitle}</p>
            </div>
          </div>
          <div className="flex gap-3">
            <Button variant="outline" className="rounded-2xl border-slate-300 bg-white">Export PDF</Button>
            <Button className="rounded-2xl bg-slate-900 text-white hover:bg-slate-800">Next Actions</Button>
          </div>
        </div>
      </div>

      <MetricGrid metrics={data.metrics} />

      <Tabs defaultValue="strategy" className="space-y-6">
        <TabsList className="h-auto flex-wrap justify-start gap-2 rounded-2xl bg-transparent p-0">
          <TabsTrigger value="strategy" className="rounded-xl border border-slate-200 bg-white px-4 py-2 data-[state=active]:border-slate-900 data-[state=active]:bg-slate-900 data-[state=active]:text-white">Strategy</TabsTrigger>
          <TabsTrigger value="execution" className="rounded-xl border border-slate-200 bg-white px-4 py-2 data-[state=active]:border-slate-900 data-[state=active]:bg-slate-900 data-[state=active]:text-white">Execution Blueprint</TabsTrigger>
          <TabsTrigger value="templates" className="rounded-xl border border-slate-200 bg-white px-4 py-2 data-[state=active]:border-slate-900 data-[state=active]:bg-slate-900 data-[state=active]:text-white">Templates</TabsTrigger>
          <TabsTrigger value="examples" className="rounded-xl border border-slate-200 bg-white px-4 py-2 data-[state=active]:border-slate-900 data-[state=active]:bg-slate-900 data-[state=active]:text-white">Examples</TabsTrigger>
        </TabsList>

        <TabsContent value="strategy" className="mt-0 grid gap-6 xl:grid-cols-3">
          {data.strategy.map((block) => (
            <Card key={block.title} className="rounded-3xl border-slate-200 shadow-sm">
              <CardHeader>
                <CardTitle className="font-serif text-3xl">{block.title}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {block.points.map((point) => (
                  <div key={point} className="flex items-start gap-3 text-slate-600">
                    <CheckCircle2 className="mt-0.5 h-5 w-5 flex-none text-yellow-500" />
                    <p>{point}</p>
                  </div>
                ))}
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="execution" className="mt-0">
          <Card className="rounded-3xl border-slate-200 shadow-sm">
            <CardContent className="p-6 md:p-8">
              <div className="grid gap-4 lg:grid-cols-2">
                {data.execution.map((item, idx) => (
                  <div key={item} className="flex items-start gap-4 rounded-2xl border border-slate-200 bg-white p-5">
                    <div className="grid h-9 w-9 place-items-center rounded-full bg-slate-900 text-sm font-semibold text-white">{idx + 1}</div>
                    <div>
                      <div className="font-medium text-slate-900">Execution Step {idx + 1}</div>
                      <div className="mt-1 text-slate-600">{item}</div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="templates" className="mt-0">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {data.templates.map((item) => (
              <Card key={item} className="rounded-3xl border-slate-200 shadow-sm">
                <CardContent className="flex items-center justify-between gap-4 p-6">
                  <div className="flex items-center gap-4">
                    <div className="grid h-12 w-12 place-items-center rounded-2xl bg-slate-100">
                      <ClipboardList className="h-5 w-5 text-slate-700" />
                    </div>
                    <div>
                      <div className="font-medium">{item}</div>
                      <div className="text-sm text-slate-500">Reusable and editable</div>
                    </div>
                  </div>
                  <ChevronRight className="h-5 w-5 text-slate-400" />
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="examples" className="mt-0">
          <div className="grid gap-6 lg:grid-cols-[1.4fr_1fr]">
            <Card className="rounded-3xl border-slate-200 shadow-sm">
              <CardHeader>
                <CardTitle className="font-serif text-3xl">Featured Example</CardTitle>
                <CardDescription>How this section comes to life inside the playbook.</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="rounded-[28px] border border-dashed border-slate-300 bg-slate-50 p-6">
                  <div className="flex items-center gap-3 text-sm uppercase tracking-[0.2em] text-slate-400">
                    <Layers3 className="h-4 w-4" /> Live preview concept
                  </div>
                  <div className="mt-4 font-serif text-4xl">{data.examples[0]}</div>
                  <p className="mt-4 max-w-2xl text-slate-600">
                    This screen pattern mirrors the uploaded UI direction: a restrained editorial layout, a left navigation shell, clear section tabs, and modular content cards that transform abstract strategy into action.
                  </p>
                  <div className="mt-8 grid gap-4 md:grid-cols-3">
                    <div className="rounded-2xl bg-white p-4 shadow-sm">
                      <div className="text-sm text-slate-400">Priority</div>
                      <div className="mt-2 font-semibold">High</div>
                    </div>
                    <div className="rounded-2xl bg-white p-4 shadow-sm">
                      <div className="text-sm text-slate-400">Owner</div>
                      <div className="mt-2 font-semibold">Strategy Team</div>
                    </div>
                    <div className="rounded-2xl bg-white p-4 shadow-sm">
                      <div className="text-sm text-slate-400">Status</div>
                      <div className="mt-2 font-semibold">In Progress</div>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>

            <div className="space-y-4">
              {data.examples.map((item) => (
                <Card key={item} className="rounded-3xl border-slate-200 shadow-sm">
                  <CardContent className="p-6">
                    <div className="flex items-start gap-4">
                      <div className="grid h-11 w-11 place-items-center rounded-2xl bg-yellow-100 text-yellow-700">
                        <Star className="h-5 w-5" />
                      </div>
                      <div>
                        <div className="font-medium">{item}</div>
                        <div className="mt-1 text-sm text-slate-500">Recommended for rollout in the next iteration.</div>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function TemplatesHub() {
  const packs = [
    { title: "Brand Launch Pack", icon: Palette, items: 6, color: "bg-rose-50 text-rose-600" },
    { title: "Customer Growth Pack", icon: Users, items: 7, color: "bg-blue-50 text-blue-600" },
    { title: "Offer Design Pack", icon: Package, items: 5, color: "bg-violet-50 text-violet-600" },
    { title: "Campaign Ops Pack", icon: Megaphone, items: 8, color: "bg-orange-50 text-orange-600" },
    { title: "Sales Process Pack", icon: Phone, items: 6, color: "bg-green-50 text-green-600" },
    { title: "Management Scorecards", icon: Gauge, items: 4, color: "bg-slate-100 text-slate-700" },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="font-serif text-5xl">Template Library</h1>
          <p className="mt-2 text-lg text-slate-500">Reusable assets for every part of the StrategyHub workflow</p>
        </div>
        <div className="flex gap-3">
          <Badge variant="secondary" className="rounded-full bg-slate-100 px-4 py-2 text-slate-600">36 active templates</Badge>
          <Badge variant="secondary" className="rounded-full bg-amber-100 px-4 py-2 text-amber-800">5 starter packs</Badge>
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        {packs.map((pack) => {
          const Icon = pack.icon;
          return (
            <Card key={pack.title} className="rounded-3xl border-slate-200 shadow-sm transition hover:-translate-y-1 hover:shadow-lg">
              <CardContent className="space-y-6 p-6">
                <div className={`grid h-14 w-14 place-items-center rounded-2xl ${pack.color}`}>
                  <Icon className="h-6 w-6" />
                </div>
                <div>
                  <div className="font-serif text-3xl">{pack.title}</div>
                  <div className="mt-1 text-slate-500">{pack.items} assets included</div>
                </div>
                <div className="flex items-center justify-between text-sm text-slate-500">
                  <span>Open pack</span>
                  <ArrowRight className="h-4 w-4" />
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}

export default function StrategyHubOtherScreensMockup() {
  const [active, setActive] = useState("homepage");

  const body = useMemo(() => {
    if (active === "homepage") {
      return <Homepage setActive={setActive} />;
    }

    if (active === "dashboard") {
      return (
        <Shell active={active} setActive={setActive}>
          <Dashboard setActive={setActive} />
        </Shell>
      );
    }

    if (active === "templates") {
      return (
        <Shell active={active} setActive={setActive}>
          <SectionPanel name="templates" />
          <div className="mt-6">
            <TemplatesHub />
          </div>
        </Shell>
      );
    }

    return (
      <Shell active={active} setActive={setActive}>
        <SectionPanel name={active} />
      </Shell>
    );
  }, [active]);

  return <div className="min-h-screen bg-[#efeff2]">{body}</div>;
}
