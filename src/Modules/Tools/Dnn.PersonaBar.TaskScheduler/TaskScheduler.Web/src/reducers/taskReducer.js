import {task as ActionTypes, pagination as PaginationActionTypes}  from "../constants/actionTypes";

export default function taskList(state = {
    taskStatusList: [],
    totalCount: 0
}, action) {
    switch (action.type) {
        case ActionTypes.RETRIEVED_TASK_STATUS_LIST:
            return { ...state,
                schedulingEnabled: action.data.schedulingEnabled,
                status: action.data.status,
                freeThreads: action.data.freeThreads,
                activeThreads: action.data.activeThreads,
                maxThreads: action.data.maxThreads,
                taskProcessingList: action.data.taskProcessingList,
                taskStatusList: action.data.taskStatusList,
                totalCount: action.data.totalCount
            };
        case ActionTypes.RETRIEVED_SCHEDULE_SETTINGS:
            return { ...state,
                schedulerModeOptions: action.data.schedulerModeOptions,
                schedulerMode: action.data.schedulerMode,
                schedulerDelay: action.data.schedulerDelay
            };
        case ActionTypes.UPDATED_SCHEDULE_SETTINGS:
            return { ...state,
                schedulerMode: action.data.schedulerMode,
                schedulerDelay: action.data.schedulerDelay
            };
        case ActionTypes.RETRIEVED_SCHEDULE_HISTORY:
            return { ...state,
                taskHistoryList: action.data.taskHistoryList,
                totalCount: action.data.totalCount
            };
        case ActionTypes.RETRIEVED_SCHEDULE_ITEMS:
        case ActionTypes.DELETED_SCHEDULE_ITEM:
            return { ...state,
                schedulerItemList: action.data.schedulerItemList,
                totalCount: action.data.totalCount
            };
        case ActionTypes.RETRIEVED_SERVER_LIST:
            return { ...state,
                serverList: action.data.serverList
            };
        case ActionTypes.RETRIEVED_SCHEDULE_ITEM:
            return { ...state,
                scheduleItemDetail: action.data.scheduleItemDetail
            };        
        default:
            return { ...state
            };
    }
}