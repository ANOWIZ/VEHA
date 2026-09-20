import { Hammer } from "lucide-react";

import { EmptyState, PageHeader } from "@/shared/ui/page";

/** Заглушка для разделов, реализуемых в последующих фазах. */
export function PlaceholderPage({ title, phase }: { title: string; phase?: string }) {
  return (
    <div>
      <PageHeader title={title} />
      <EmptyState
        icon={<Hammer className="h-8 w-8" />}
        title="Раздел в разработке"
        description={phase ? `Будет реализован в рамках фазы: ${phase}.` : undefined}
      />
    </div>
  );
}
