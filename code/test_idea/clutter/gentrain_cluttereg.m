function [ X, T, Xout, Tout ] = gentrain_cluttereg( op)
%GENTRAIN_CLUTTEREG Generate training set (messages) for clutter_egauss.
% 
% T = theta
% Tout = outgoing messages for theta (after projection)
% 

if nargin < 1
    op = [];
end

N = myProcessOptions(op, 'training_size', 1000);


% gen X, gen T
X = gen_x_msg(N);
T = gen_theta_msg(N);

% proposal distribution for for the conditional varibles (i.e. t) 
% in the factor. Require: Sampler & Density.
op.in_proposal = DistNormal( 3, 30);

% clutter problem specific parameters
a = myProcessOptions(op, 'clutter_a', 10);
w = myProcessOptions(op, 'clutter_w', 0.5);

% A forward sampling function taking samples (array) from in_proposal and
% outputting samples from the conditional distribution represented by the
% factor.
op.cond_factor = @(T)(ClutterMinka.x_cond_dist(T, a, w));
 

 [ X, T, Xout, Tout ] = gentrain_dist2(X, T, op);
 
end


function X=gen_x_msg(n)
% 
M = randn(1, n)*20 + 3;
% V = gamrnd(2, 1, 1, n);
V = unifrnd(0.05, 1000, 1, n);
X = DistNormal(M, V);
end


function T=gen_theta_msg(n)
MT = unifrnd(-10, 10, 1, n);
% VT = gamrnd(2, 1, 1, n);
VT = unifrnd(0.05, 1000, 1, n);
T = DistNormal(MT, VT);
end
