export type Post = {
  channelTag: string;
  messageId: number;
  postedAt: string;
  text: string | null;
  views: number | null;
  mediaUrls: string[];
  forwardFrom: string | null;
  isEdited: boolean | null;
  url: string;
};
export type Page = {
  page: number;
  pageSize: number;
  hasNextPage: boolean;
  items: Post[];
};
export type Filters = { channel: string; from: string; to: string; q: string };
