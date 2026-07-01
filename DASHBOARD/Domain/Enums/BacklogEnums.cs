namespace DASHBOARD.Domain.Enums;

/// <summary>Hierarchy level of a backlog item (Epic → Feature → UserStory).</summary>
public enum BacklogItemType { Epic, Feature, UserStory }

/// <summary>Refinement lifecycle state of a backlog item.</summary>
public enum BacklogItemState { New, Refining, Ready, Committed }

/// <summary>T-shirt sizing for rough backlog estimation.</summary>
public enum TshirtSize { XS, S, M, L, XL }
