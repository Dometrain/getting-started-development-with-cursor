import type { ReactElement } from 'react';
import { Bar, BarChart, ResponsiveContainer, XAxis, YAxis } from 'recharts';

export type PollChartBar = {
  option: string;
  votes: number;
};

type PollChartProps = {
  data: PollChartBar[];
};

export function PollChart({ data }: PollChartProps): ReactElement {
  return (
    <ResponsiveContainer width="100%" height={240}>
      <BarChart data={data}>
        <XAxis dataKey="option" />
        <YAxis allowDecimals={false} />
        <Bar dataKey="votes" isAnimationActive={false} />
      </BarChart>
    </ResponsiveContainer>
  );
}
