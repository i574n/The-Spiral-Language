import torch
import torch.distributions
import torch.optim
import torch.nn
import torch.linalg
from torch.functional import Tensor
from torch.nn import Module

def control(state_probs : Tensor,head : Tensor,action_indices : Tensor,at_action_value : Tensor,at_action_weights : Tensor):
    # state_probs[batch_dim,state_dim]
    # head[action_dim*2,state_dim]
    # action_indices[batch_dim] : map (action_dim -> batch_dim)
    # at_action_value[batch_dim,1] : map (action_dim -> batch_dim)
    # at_action_weight[batch_dim,1] : map (action_dim -> batch_dim)
    num_actions = head.shape[0]//2
    head_weighted_values = head[:num_actions,:] # [action_dim,state_dim]
    head_value_weights = head[num_actions:,:] # [action_dim,state_dim]

    def update_head(): # Weighted moving average update. Works well with probabilistic vectors and weighted updates that CFR requires.
        state_weights = at_action_weights * state_probs # [batch_dim,state_dim]
        head_weighted_values[action_indices,:] += at_action_value * state_weights
        head_value_weights[action_indices,:] += state_weights

    def calculate():
        values = head_weighted_values / head_value_weights # [action_dim,state_dim]
        def state_probs_grad(): # Prediction errors modulate the state probabilities. The cool part is the centering.
            prediction_values_for_state = values[action_indices,:] # [batch_dim,state_dim]
            prediction_errors = torch.abs(at_action_value - prediction_values_for_state) # [batch_dim,state_dim]
            prediction_error_mean = (state_probs * prediction_errors).sum(-1,keepdim=True) # [batch_dim,1]
            return at_action_weights * (prediction_errors - prediction_error_mean) # [batch_dim,state_dim]

        def action_probs(action_probs : Tensor, sample_probs : Tensor): # Implements the VR MC-CFR update. Could be easily adapted to train an ensemble of actors.
            # action_probs[batch_dim,action_dim]
            # sample_probs[batch_dim,action_dim]
            prediction_values_for_action = state_probs.mm(values.t()) # [batch_dim,action_dim]
            at_action_sample_probs = torch.gather(sample_probs,-1,action_indices.unsqueeze(-1)) # [batch_dim,1]
            at_action_prediction_value = torch.gather(prediction_values_for_action,-1,action_indices.unsqueeze(-1)) # [batch_dim,1]
            at_action_prediction_adjustmnet = (at_action_value - at_action_prediction_value) / at_action_sample_probs # [batch_dim,1]
            torch.scatter_add(prediction_values_for_action,-1,action_indices.unsqueeze(-1),at_action_prediction_adjustmnet)
            reward = (action_probs * prediction_values_for_action).sum(-1,keepdim=True) # [batch_dim,1]
            def grad(): return -at_action_weights * (prediction_values_for_action - reward) # [batch_dim,action_dim]
            return reward, grad
        return state_probs_grad, action_probs
    return update_head, calculate

def optimize(moduleList : list,learning_rate : float = 2 ** -7,signSGDfactor : float = 2 ** -3):
    """
    Interpolates between signSGD and infinity norm normalization.
    signSGDfactor - The interpolation factor for signSGD. 0 is pure infinity norm normalization, while 1 is pure signSGD.
    """
    assert (0 <= signSGDfactor <= 1)
    assert (0 <= learning_rate)
    with torch.no_grad():
        for module in moduleList:
            infNorm = torch.scalar_tensor(0)
            paramGroup = [x for x in module.parameters() if x.grad is not None]
            for x in paramGroup:
                if x.grad: infNorm = torch.max(infNorm,torch.linalg.norm(x.grad.flatten(),ord=float('inf')))
            for x in paramGroup: # The scalars operations are grouped for efficiency.
                if torch.is_nonzero(infNorm):
                    x += learning_rate * signSGDfactor * torch.sign(x.grad) + learning_rate * (1 - signSGDfactor) / infNorm * x.grad
                    x.grad = None

def policy(value : Module,head : Tensor,policy : Module,is_update_head : bool,is_update_value : bool,is_update_policy : bool,
        epsilon : float,input_policy : Tensor,input_value : Tensor,action_mask : Tensor):
    action_raw = torch.masked_fill(policy(input_policy),action_mask,float('-inf'))
    action_log_probs : Tensor = torch.log_softmax(action_raw)
    action_probs = torch.exp(action_log_probs.detach())
    action_mask_inv = torch.logical_not(action_mask)
    # Interpolates action probs with an uniform noise distribution for actions that aren't masked out.
    sample_probs = action_mask_inv / (action_mask_inv.sum(-1) * (1 / epsilon)) + action_probs * (1 - epsilon)
    sample_indices = torch.distributions.Categorical(sample_probs).sample()
    def back(rewards : Tensor, regret_probs : Tensor):
        value_log_probs : Tensor = torch.log_softmax(value(input_value))
        value_probs = torch.exp(value_log_probs.detach())
        update_head, calculate = control(value_probs,head,sample_indices,rewards,regret_probs)
        state_probs_grad, action_probs = calculate()
        reward, action_probs_grad = action_probs(action_probs,sample_probs)
        if is_update_head: update_head()
        if is_update_value: value_log_probs.backward(state_probs_grad())
        if is_update_policy: action_log_probs.backward(action_probs_grad())
        return reward
    return (torch.log(action_probs), torch.log(sample_probs), sample_indices), back